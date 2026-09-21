using System.Text;
using System.Text.RegularExpressions;

namespace Absensi.Data.Diagnostik;

// docs/INVESTIGASI-CRASH-2026-09-21.md §4: satu-satunya kelas di seluruh
// solusi yang tahu format baris log, lokasi berkas, dan aturan sanitasi.
// Ditaruh di Absensi.Data (bukan proyek baru) karena Absensi.App DAN
// Absensi.Telegram sudah mereferensikan Absensi.Data hari ini — menaruh di
// sini membuatnya bisa dipakai keduanya tanpa referensi baru dan tanpa
// risiko circular dependency (§4.1). Absensi.Domain tidak disentuh sama
// sekali dan tetap nol referensi keluar.
//
// Prinsip yang mengikat (§4, ditegaskan ulang oleh pemilik proyek):
// kelas ini TIDAK PERNAH melempar exception ke pemanggilnya, apa pun yang
// terjadi saat menulis — kegagalan mencatat tidak boleh menjadi exception
// BARU yang harus ditangani lagi (lihat §4.7.3).
//
// Waktu: baris log memakai DateTimeOffset.Now langsung, BUKAN
// IPenyediaWaktu (CLAUDE.md §5 "Waktu" berlaku untuk keputusan ABSENSI yang
// perlu deterministik saat diuji — timestamp diagnostik di log galat bukan
// aturan bisnis yang pernah diuji nilainya, sama seperti timestamp Windows
// Event Log tidak melalui abstraksi aplikasi). Ini pengecualian yang
// disengaja dan disebutkan eksplisit di sini, bukan kelalaian. Alasan lain:
// Absensi.Telegram (salah satu pemanggil) tidak memegang IPenyediaWaktu
// sama sekali, dan menambah parameter itu ke KlienBotTelegram berarti
// mengubah konstruktornya (dan seluruh call site pengujiannya) untuk
// sesuatu yang bukan aturan bisnis — di luar cakupan "hati-hati" yang
// diminta.
public static class PencatatGalat
{
    private const int JumlahBerkasResmiDisimpan = 30;
    private const int KedalamanInnerExceptionMaksimum = 5;

    // Batas JUMLAH TOTAL exception yang dicatat dalam satu entri (beda dari
    // kedalaman) — AggregateException.InnerExceptions bisa berisi ratusan
    // item pada kasus fan-out ekstrem (mis. Task.WhenAll atas banyak
    // pekerjaan). Tanpa batas ini, satu entri log bisa membengkak tak wajar.
    private const int JumlahTotalExceptionMaksimum = 20;

    private const string NamaBerkasCadangan = "_gagal-tulis.log";

    // §D.2: berkas cadangan bukan log utama — kalau melewati batas ini,
    // ditimpa (reset), bukan dirotasi seperti berkas resmi. Sengaja jauh
    // lebih sederhana dari Rotasi() karena jalur ini hanya terpakai saat
    // jalur utama SUDAH gagal (§4.7.3).
    private const long UkuranMaksimalBerkasCadangan = 1 * 1024 * 1024;

    private static readonly Regex PolaNamaBerkasResmi = new(@"^\d{8}\.log$", RegexOptions.Compiled);

    // Path lokal Windows umumnya "C:\Users\<akun>\..." — akun bisa jadi
    // nama asli developer/tester yang terekam di PDB/symbol saat build.
    private static readonly Regex PolaPathAkunWindows =
        new(@"[A-Za-z]:\\Users\\[^\\""]+", RegexOptions.Compiled);

    // Bentuk umum token Bot Telegram: <id numerik>:<string alfanumerik panjang>.
    private static readonly Regex PolaTokenTelegram =
        new(@"\d{6,}:[A-Za-z0-9_-]{30,}", RegexOptions.Compiled);

    // "Data Source=...", "Password=...", "Pwd=..." pada connection string.
    private static readonly Regex PolaConnectionString =
        new(@"(Data Source|Password|Pwd)\s*=\s*[^;""]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Deretan 10-18 digit berurutan: bentuk umum NISN/NIP/nomor telepon.
    private static readonly Regex PolaAngkaPanjang =
        new(@"(?<!\d)\d{10,18}(?!\d)", RegexOptions.Compiled);

    private const string PlaceholderPesanDisembunyikan =
        "[pesan disembunyikan — berasal dari lapisan data sekolah, lihat jenis exception dan stack trace]";

    // docs/INVESTIGASI-CRASH-2026-09-21.md §4.1: dipakai LokasiData.FolderLog
    // di Absensi.App supaya satu sumber kebenaran path log tidak terduplikasi
    // di dua proyek — Absensi.Telegram (yang TIDAK BOLEH mereferensikan
    // Absensi.App) memakai properti ini langsung lewat Absensi.Data yang
    // sudah jadi referensinya.
    public static string FolderLogBawaan => Path.Combine(AppContext.BaseDirectory, "logs");

    /// <summary>
    /// Mencatat satu exception ke berkas log harian. Tidak pernah melempar.
    /// </summary>
    /// <param name="exception">Exception yang dicatat.</param>
    /// <param name="sumber">Nama Command/method/handler tempat exception ditangkap (BUKAN lokasi asli — itu sudah ada di stack trace).</param>
    /// <param name="level">"FATAL" untuk titik yang berujung aplikasi ditutup, "ERROR" untuk titik yang aplikasi tetap berjalan.</param>
    /// <param name="folderLog">Override lokasi folder log, dipakai pengujian. Default: <see cref="FolderLogBawaan"/>.</param>
    public static void Catat(Exception exception, string sumber, string level = "ERROR", string? folderLog = null)
    {
        var folder = folderLog ?? FolderLogBawaan;

        try
        {
            Directory.CreateDirectory(folder);
            var baris = BangunBaris(exception, sumber, level);
            TulisBaris(folder, baris);
        }
        catch (Exception galatTulis)
        {
            // §4.7.3: TIDAK memanggil Catat(...) lagi di sini — itu akan
            // mengulang persis operasi yang baru saja gagal (rekursi kalau
            // penyebabnya menetap, mis. folder read-only). Jalur cadangan
            // di bawah sengaja jauh lebih sederhana dari jalur utama.
            TulisCadangan(folder, sumber, galatTulis);
            return;
        }

        try
        {
            Rotasi(folder);
        }
        catch (Exception galatRotasi)
        {
            try
            {
                TulisBaris(folder, BangunBarisRotasiGagal(galatRotasi, sumber));
            }
            catch
            {
                // §4.7.3: satu-satunya penelanan diam-diam yang disengaja di
                // seluruh rancangan ini. Sudah dua lapis percobaan pencatatan
                // gagal (baris rotasi + cadangan) — tidak ada tempat lain
                // yang lebih aman untuk melaporkannya tanpa risiko rekursi,
                // dan kegagalan mencatat TIDAK BOLEH menghalangi aplikasi
                // menyelesaikan penanganan exception ASLI-nya.
            }
        }
    }

    private static void TulisBaris(string folder, string baris)
    {
        var path = Path.Combine(folder, $"{DateTimeOffset.Now:yyyyMMdd}.log");
        File.AppendAllText(path, baris);
    }

    private static void TulisCadangan(string folder, string sumber, Exception galatAsli)
    {
        try
        {
            var pathCadangan = Path.Combine(folder, NamaBerkasCadangan);
            var baris = $"[{DateTimeOffset.Now:O}] Gagal menulis log untuk sumber '{sumber}': {galatAsli.GetType().FullName}{Environment.NewLine}";

            // §D.2: reset (timpa), bukan tambah, kalau berkas cadangan sudah
            // mencapai batas — mekanisme paling sederhana yang cukup untuk
            // berkas yang seharusnya jarang dipakai sama sekali.
            if (File.Exists(pathCadangan) && new FileInfo(pathCadangan).Length >= UkuranMaksimalBerkasCadangan)
            {
                File.WriteAllText(pathCadangan, baris);
            }
            else
            {
                File.AppendAllText(pathCadangan, baris);
            }
        }
        catch
        {
            // §4.7.3: kegagalan menulis berkas CADANGAN sendiri ditelan
            // diam-diam sepenuhnya di sini, TANPA memanggil PencatatGalat
            // atau TulisCadangan lagi — itu akan jadi logging rekursif
            // persis yang dilarang.
        }
    }

    // Nama berkas ini SENGAJA tidak cocok pola yyyyMMdd.log (lihat
    // NamaBerkasCadangan) — supaya rotasi (yang hanya melihat pola resmi)
    // tidak pernah menganggapnya kandidat hapus maupun kandidat retensi.
    private static string BangunBaris(Exception exception, string sumber, string level)
    {
        var sb = new StringBuilder();
        var waktu = DateTimeOffset.Now;
        sb.Append('[').Append(waktu.ToString("yyyy-MM-ddTHH:mm:sszzz")).Append("] [")
          .Append(level).Append("] [").Append(sumber).Append(']').AppendLine();

        // §A.1: HashSet berbasis identitas objek (bukan Equals/GetHashCode
        // override milik Exception, yang tidak ada) mencegah rekursi tanpa
        // batas kalau suatu saat ada rantai InnerException yang bersiklus.
        // jumlahTercatat membatasi TOTAL exception per entri, terpisah dari
        // kedalamanMaksimum — relevan untuk AggregateException yang bisa
        // punya banyak InnerExceptions SEJAJAR (lebar), bukan cuma rantai
        // yang dalam.
        var sudahDicatat = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        var jumlahTercatat = 0;
        TulisSatuException(sb, exception, kedalaman: 0, sudahDicatat, ref jumlahTercatat);
        sb.AppendLine("--- akhir entri ---");
        return sb.ToString();
    }

    private static string BangunBarisRotasiGagal(Exception galatRotasi, string sumberAsli) =>
        $"[{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:sszzz}] [ERROR] [Rotasi log setelah mencatat '{sumberAsli}'] " +
        $"{galatRotasi.GetType().FullName}: {SanitasiPesanUmum(galatRotasi.Message)}{Environment.NewLine}--- akhir entri ---{Environment.NewLine}";

    private static void TulisSatuException(
        StringBuilder sb, Exception? exception, int kedalaman, HashSet<Exception> sudahDicatat, ref int jumlahTercatat)
    {
        if (exception is null || kedalaman > KedalamanInnerExceptionMaksimum || jumlahTercatat >= JumlahTotalExceptionMaksimum)
        {
            return;
        }

        if (!sudahDicatat.Add(exception))
        {
            // §A.1: exception yang SAMA (identitas objek, bukan sekadar
            // jenis yang sama) sudah pernah dicatat di rantai ini —
            // berhenti di sini alih-alih rekursi tanpa batas.
            sb.AppendLine("   (dilewati — exception yang sama sudah dicatat sebelumnya di rantai/pohon ini)");
            return;
        }

        jumlahTercatat++;
        var awalan = kedalaman == 0 ? string.Empty : new string(' ', kedalaman * 2) + "InnerException: ";

        // §A.1: AggregateException.Message MENYUSUN ULANG teks seluruh
        // InnerExceptions ke dalam Message miliknya sendiri secara bawaan
        // .NET — tidak pernah aman dicatat apa adanya walau Source milik
        // AggregateException itu sendiri bukan Absensi.Domain/Data. Rincian
        // aslinya tetap tercatat lewat rekursi ke tiap InnerExceptions di
        // bawah, MASING-MASING dengan aturan sanitasinya sendiri.
        string pesan;
        if (exception is AggregateException agregat)
        {
            pesan = $"(AggregateException, {agregat.InnerExceptions.Count} InnerException — lihat rincian di bawah, BUKAN Message gabungan bawaan .NET)";
        }
        else
        {
            pesan = HarusSembunyikanPesan(exception)
                ? PlaceholderPesanDisembunyikan
                : SanitasiPesanUmum(exception.Message);
        }

        sb.Append(awalan).Append(exception.GetType().FullName).Append(": ").AppendLine(pesan);
        sb.Append("   HResult=").Append(exception.HResult)
          .Append(", Source=").Append(SanitasiJejak(exception.Source))
          .Append(", TargetSite=").AppendLine(exception.TargetSite?.Name ?? "-");
        sb.AppendLine(SanitasiJejak(exception.StackTrace) ?? "(tidak ada stack trace)");

        if (exception is AggregateException agregatUntukRekursi)
        {
            foreach (var dalam in agregatUntukRekursi.InnerExceptions)
            {
                if (jumlahTercatat >= JumlahTotalExceptionMaksimum)
                {
                    sb.AppendLine("   ... (dipotong — terlalu banyak InnerExceptions untuk satu entri)");
                    break;
                }

                TulisSatuException(sb, dalam, kedalaman + 1, sudahDicatat, ref jumlahTercatat);
            }
        }
        else
        {
            TulisSatuException(sb, exception.InnerException, kedalaman + 1, sudahDicatat, ref jumlahTercatat);
        }
    }

    // §4.7.2: exception yang SUMBER-nya (assembly yang melempar, lewat
    // Exception.Source) adalah lapisan yang menyentuh data sekolah —
    // Message-nya disembunyikan SECARA DEFAULT sampai ada audit terpisah
    // yang memverifikasi kelas exception tertentu tidak pernah menyisipkan
    // NISN/NIP/nama langsung di teks pesannya. Exception.Source dipakai
    // (bukan namespace TIPE exception-nya) karena banyak galat domain
    // dilempar sebagai tipe BCL biasa (InvalidOperationException,
    // ArgumentException) dengan pesan custom — namespace tipe-nya "System",
    // bukan "Absensi.Data", jadi tidak bisa dipakai sebagai penanda.
    private static bool HarusSembunyikanPesan(Exception exception)
    {
        var sumberAssembly = exception.Source ?? string.Empty;
        return sumberAssembly.StartsWith("Absensi.Domain", StringComparison.Ordinal)
            || sumberAssembly.StartsWith("Absensi.Data", StringComparison.Ordinal);
    }

    private static string SanitasiPesanUmum(string? pesan)
    {
        if (string.IsNullOrEmpty(pesan))
        {
            return string.Empty;
        }

        var hasil = PolaConnectionString.Replace(pesan, m => $"{m.Groups[1].Value}=<redaksi>");
        hasil = PolaTokenTelegram.Replace(hasil, "<token-redaksi>");
        hasil = PolaAngkaPanjang.Replace(hasil, "<angka-redaksi>");
        return SanitasiJejak(hasil) ?? hasil;
    }

    private static string? SanitasiJejak(string? teks) =>
        teks is null ? null : PolaPathAkunWindows.Replace(teks, @"<profil-pengguna>");

    // Retensi §4.7.4: HANYA berkas yang namanya cocok persis pola
    // yyyyMMdd.log masuk daftar kandidat hapus. Berkas apa pun dengan nama
    // lain (termasuk NamaBerkasCadangan di atas, atau berkas yang ditaruh
    // manual oleh siapa pun) tidak pernah masuk daftar ini sama sekali —
    // bukan sekadar "dilewati", tapi memang tidak pernah dilihat sebagai
    // kandidat. Urutan nama yyyyMMdd = urutan kronologis, sama seperti pola
    // LayananBackup.HapusBerkasSelainNTerbaru yang sudah ada.
    private static void Rotasi(string folder)
    {
        var berkasLama = Directory.GetFiles(folder)
            .Where(path => PolaNamaBerkasResmi.IsMatch(Path.GetFileName(path)))
            .OrderByDescending(path => path)
            .Skip(JumlahBerkasResmiDisimpan);

        foreach (var path in berkasLama)
        {
            File.Delete(path);
        }
    }
}

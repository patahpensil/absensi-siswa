using System.Globalization;
using System.Text.RegularExpressions;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.MasterData;

// Impor Excel siswa mengikuti docs/PRD.md §16.3 persis: analisis (langkah
// 3), validasi per baris (langkah 4), lalu impor dalam satu transaksi
// (langkah 8) — "utuh": bila ada baris bermasalah, tidak ada yang
// tersimpan sama sekali. Menampilkan Error/Warning, Preview, dan
// Konfirmasi (langkah 5-7) adalah urusan layar (Absensi.App), bukan kelas
// ini — kelas ini hanya menganalisis dan mengeksekusi.
public sealed class ImporSiswaExcel
{
    // docs/PRD.md §5.4: sembilan kolom baku, urutan tetap.
    // A-12: internal (bukan public) + IReadOnlyList (bukan string[]) --
    // pembuat template (PembuatTemplateExcel) ada di assembly yang sama,
    // dan test (Absensi.Data.Tests) diberi akses lewat
    // [assembly: InternalsVisibleTo] di AssemblyInfo.cs, bukan dengan
    // melebarkan visibilitas jadi public. Array.AsReadOnly (bukan sekadar
    // deklarasi tipe IReadOnlyList di atas array mentah) supaya objek
    // RUNTIME-nya sendiri (ReadOnlyCollection<T>) menolak mutasi lewat
    // NotSupportedException walau di-cast ke IList<string> -- deklarasi
    // tipe saja hanya membatasi di compile time, tidak di runtime.
    internal static readonly IReadOnlyList<string> KolomBaku = Array.AsReadOnly(new[]
    {
        "NISN", "NAMA_LENGKAP", "JENIS_KELAMIN", "TEMPAT_LAHIR",
        "TANGGAL_LAHIR", "ALAMAT", "KELAS", "NAMA_ORANG_TUA", "NO_HP_ORANG_TUA",
    });

    private static readonly Regex PolaNoHp = new(@"^\+?[0-9]{9,15}$", RegexOptions.Compiled);

    private readonly RepositoriSiswa _repositoriSiswa = new();
    private readonly RepositoriKelas _repositoriKelas = new();
    private readonly RepositoriSiswaKelas _repositoriSiswaKelas = new();
    private readonly KartuImporExcel _kartu = new(JenisPemilikKartu.Siswa);
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    // PRD §16.3 langkah 3-5: baca berkas, validasi tiap baris, kembalikan
    // nomor baris + alasan untuk yang bermasalah.
    public HasilAnalisisImporSiswa Analisis(SqliteConnection connection, int tahunAjaranId, string pathBerkas)
    {
        using var stream = File.OpenRead(pathBerkas);
        return Analisis(connection, tahunAjaranId, stream);
    }

    public HasilAnalisisImporSiswa Analisis(SqliteConnection connection, int tahunAjaranId, Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault()
            ?? throw new FormatException("Berkas Excel ini tidak punya lembar kerja (sheet) sama sekali.");

        for (var kolom = 0; kolom < KolomBaku.Count; kolom++)
        {
            var judul = worksheet.Cell(1, kolom + 1).GetString().Trim();
            if (!string.Equals(judul, KolomBaku[kolom], StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException(
                    $"Kolom {kolom + 1} seharusnya '{KolomBaku[kolom]}', berkas ini punya '{judul}'.");
            }
        }

        var kolomKartu = KartuImporExcel.CariKolom(worksheet, KolomBaku.Count);
        var uidDalamBerkas = new HashSet<string>(StringComparer.Ordinal);
        var hasil = new List<BarisImporSiswa>();
        var nisnDalamBerkas = new HashSet<string>();

        foreach (var row in worksheet.RowsUsed().Where(row => row.RowNumber() > 1).OrderBy(row => row.RowNumber()))
        {
            var nomorBaris = row.RowNumber();

            var nisn = Teks(worksheet, nomorBaris, 1);
            var namaLengkap = Teks(worksheet, nomorBaris, 2);
            var jenisKelamin = Teks(worksheet, nomorBaris, 3);
            var tempatLahir = Teks(worksheet, nomorBaris, 4);
            var selTanggalLahir = worksheet.Cell(nomorBaris, 5);
            var tanggalLahir = selTanggalLahir.DataType == XLDataType.DateTime
                ? selTanggalLahir.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : selTanggalLahir.GetString().Trim();
            var alamat = Teks(worksheet, nomorBaris, 6);
            var kelas = Teks(worksheet, nomorBaris, 7);
            var namaOrangTua = Teks(worksheet, nomorBaris, 8);
            var noHpOrangTua = Teks(worksheet, nomorBaris, 9);

            var kesalahan = new List<string>();

            // 4.1 NISN wajib, 10 digit, tidak duplikat di dalam file
            if (string.IsNullOrWhiteSpace(nisn))
            {
                kesalahan.Add("NISN wajib diisi");
            }
            else if (nisn.Length != 10 || !nisn.All(char.IsDigit))
            {
                kesalahan.Add("NISN harus 10 digit angka");
            }
            else if (!nisnDalamBerkas.Add(nisn))
            {
                kesalahan.Add("NISN duplikat di dalam berkas");
            }

            // 4.3 NAMA_LENGKAP wajib
            if (string.IsNullOrWhiteSpace(namaLengkap))
            {
                kesalahan.Add("Nama lengkap wajib diisi");
            }

            // 4.4 JENIS_KELAMIN hanya L atau P
            if (jenisKelamin is not ("L" or "P"))
            {
                kesalahan.Add("Jenis kelamin harus L atau P");
            }

            // 4.5 TANGGAL_LAHIR format YYYY-MM-DD bila diisi
            if (!string.IsNullOrWhiteSpace(tanggalLahir) && !DateOnly.TryParseExact(tanggalLahir, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                kesalahan.Add("Tanggal lahir harus berformat YYYY-MM-DD");
            }

            // 4.6 KELAS wajib, harus ada di Master Kelas
            if (string.IsNullOrWhiteSpace(kelas))
            {
                kesalahan.Add("Kelas wajib diisi");
            }
            else if (_repositoriKelas.CariIdBerdasarkanNama(connection, tahunAjaranId, kelas) is null)
            {
                kesalahan.Add($"Kelas '{kelas}' tidak ditemukan di Master Kelas");
            }

            // 4.7 NO_HP_ORANG_TUA format nomor valid bila diisi
            if (!string.IsNullOrWhiteSpace(noHpOrangTua) && !PolaNoHp.IsMatch(noHpOrangTua))
            {
                kesalahan.Add("Nomor HP orang tua tidak valid");
            }

            var id = _repositoriSiswa.CariIdBerdasarkanNisn(connection, nisn);
            var uid = _kartu.Baca(connection, worksheet, nomorBaris, kolomKartu, id, uidDalamBerkas, kesalahan);

            hasil.Add(new BarisImporSiswa(
                nomorBaris,
                nisn,
                namaLengkap,
                jenisKelamin,
                string.IsNullOrWhiteSpace(tempatLahir) ? null : tempatLahir,
                string.IsNullOrWhiteSpace(tanggalLahir) ? null : tanggalLahir,
                string.IsNullOrWhiteSpace(alamat) ? null : alamat,
                kelas,
                string.IsNullOrWhiteSpace(namaOrangTua) ? null : namaOrangTua,
                string.IsNullOrWhiteSpace(noHpOrangTua) ? null : noHpOrangTua,
                kesalahan,
                SudahAdaDiDatabase: kesalahan.Count == 0 && id is not null,
                NoKartu: uid));
        }

        return new HasilAnalisisImporSiswa(hasil);
    }

    // PRD §16.3 langkah 8: NISN sudah ada → diperbarui, NISN baru →
    // ditambahkan, siswa di database yang tak ada di file → dibiarkan.
    // Satu transaksi untuk semua baris (docs/PRD.md §16.3: "impor bersifat
    // utuh").
    public HasilImporSiswa Impor(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        int tahunAjaranId,
        IReadOnlyList<BarisImporSiswa> barisTervalidasi)
    {
        AuthorizationGuard.EnsureFullAuthority(akun, "mengimpor data siswa");

        if (barisTervalidasi.Any(b => !b.Sah))
        {
            throw new InvalidOperationException("Tidak bisa mengimpor selagi masih ada baris bermasalah.");
        }

        var ditambah = 0;
        var diperbarui = 0;

        var kartuDitautkan = 0;
        var kartuTetap = 0;

        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "impor_siswa",
            entitas: "siswa",
            perubahan: transaction =>
            {
                foreach (var baris in barisTervalidasi)
                {
                    var idSiswaAda = _repositoriSiswa.CariIdBerdasarkanNisn(connection, baris.Nisn!, transaction);
                    var idKelas = _repositoriKelas.CariIdBerdasarkanNama(connection, tahunAjaranId, baris.Kelas!, transaction)
                        ?? throw new InvalidOperationException($"Baris {baris.NomorBaris}: kelas tidak lagi tersedia.");
                    var data = new DataSiswa(
                        baris.Nisn!,
                        baris.NamaLengkap!,
                        baris.JenisKelamin!,
                        baris.TempatLahir,
                        baris.TanggalLahir,
                        baris.Alamat,
                        baris.NamaOrangTua,
                        baris.NoHpOrangTua);

                    int idSiswa;
                    if (idSiswaAda is { } id)
                    {
                        _repositoriSiswa.Perbarui(connection, transaction, id, data);
                        idSiswa = id;
                        diperbarui++;
                    }
                    else
                    {
                        idSiswa = _repositoriSiswa.Tambah(connection, transaction, data);
                        ditambah++;
                    }

                    _repositoriSiswaKelas.Tetapkan(connection, transaction, idSiswa, idKelas, tahunAjaranId);
                    _kartu.Tautkan(connection, transaction, baris.NoKartu, idSiswa, waktu, baris.NomorBaris, ref kartuDitautkan, ref kartuTetap);
                }

                return new CatatanPerubahan(null, null,
                    $"{ditambah} ditambah, {diperbarui} diperbarui, {kartuDitautkan} kartu ditautkan, {kartuTetap} kartu tetap");
            });

        return new HasilImporSiswa(ditambah, diperbarui) { KartuDitautkan = kartuDitautkan, KartuTetap = kartuTetap };
    }

    public byte[] BuatTemplate() => PembuatTemplateExcel.Bangun("Siswa", [.. KolomBaku, KartuImporExcel.NamaKolom]);

    private static string Teks(IXLWorksheet worksheet, int baris, int kolom) =>
        worksheet.Cell(baris, kolom).GetString().Trim();
}

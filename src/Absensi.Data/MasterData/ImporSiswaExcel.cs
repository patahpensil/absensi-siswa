using System.Text.RegularExpressions;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
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
    private static readonly string[] KolomBaku =
    {
        "NISN", "NAMA_LENGKAP", "JENIS_KELAMIN", "TEMPAT_LAHIR",
        "TANGGAL_LAHIR", "ALAMAT", "KELAS", "NAMA_ORANG_TUA", "NO_HP_ORANG_TUA",
    };

    private static readonly Regex PolaTanggal = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);
    private static readonly Regex PolaNoHp = new(@"^\+?[0-9]{9,15}$", RegexOptions.Compiled);

    private readonly RepositoriSiswa _repositoriSiswa = new();
    private readonly RepositoriKelas _repositoriKelas = new();
    private readonly RepositoriSiswaKelas _repositoriSiswaKelas = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    // PRD §16.3 langkah 3-5: baca berkas, validasi tiap baris, kembalikan
    // nomor baris + alasan untuk yang bermasalah.
    public HasilAnalisisImporSiswa Analisis(SqliteConnection connection, int tahunAjaranId, string pathBerkas)
    {
        using var workbook = new XLWorkbook(pathBerkas);
        var worksheet = workbook.Worksheets.First();

        for (var kolom = 0; kolom < KolomBaku.Length; kolom++)
        {
            var judul = worksheet.Cell(1, kolom + 1).GetString().Trim();
            if (!string.Equals(judul, KolomBaku[kolom], StringComparison.OrdinalIgnoreCase))
            {
                throw new FormatException(
                    $"Kolom {kolom + 1} seharusnya '{KolomBaku[kolom]}', berkas ini punya '{judul}'.");
            }
        }

        var barisTerakhir = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var hasil = new List<BarisImporSiswa>();
        var nisnDalamBerkas = new HashSet<string>();

        for (var nomorBaris = 2; nomorBaris <= barisTerakhir; nomorBaris++)
        {
            if (worksheet.Row(nomorBaris).IsEmpty())
            {
                continue;
            }

            var nisn = Teks(worksheet, nomorBaris, 1);
            var namaLengkap = Teks(worksheet, nomorBaris, 2);
            var jenisKelamin = Teks(worksheet, nomorBaris, 3);
            var tempatLahir = Teks(worksheet, nomorBaris, 4);
            var tanggalLahir = Teks(worksheet, nomorBaris, 5);
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
            if (!string.IsNullOrWhiteSpace(tanggalLahir) && !PolaTanggal.IsMatch(tanggalLahir))
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
                SudahAdaDiDatabase: kesalahan.Count == 0 &&
                    _repositoriSiswa.CariIdBerdasarkanNisn(connection, nisn) is not null));
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
        if (barisTervalidasi.Any(b => !b.Sah))
        {
            throw new InvalidOperationException("Tidak bisa mengimpor selagi masih ada baris bermasalah.");
        }

        var rencana = barisTervalidasi
            .Select(baris => (
                Baris: baris,
                IdSiswaAda: _repositoriSiswa.CariIdBerdasarkanNisn(connection, baris.Nisn!),
                IdKelas: _repositoriKelas.CariIdBerdasarkanNama(connection, tahunAjaranId, baris.Kelas!)!.Value))
            .ToList();

        var ditambah = 0;
        var diperbarui = 0;

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "impor_siswa",
            entitas: "siswa",
            perubahan: transaction =>
            {
                foreach (var (baris, idSiswaAda, idKelas) in rencana)
                {
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
                }

                return null;
            },
            nilaiBaru: $"{ditambah} ditambah, {diperbarui} diperbarui");

        return new HasilImporSiswa(ditambah, diperbarui);
    }

    private static string Teks(IXLWorksheet worksheet, int baris, int kolom) =>
        worksheet.Cell(baris, kolom).GetString().Trim();
}

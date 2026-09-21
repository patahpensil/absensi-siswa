using System.IO;
using Absensi.App.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using ClosedXML.Excel;

namespace Absensi.App.Tests;

// Master Bug & UI/UX Register A-8 (docs/TODO-VERSI-BERIKUTNYA.md A.3.2):
// ImporGuruWizardViewModel/ImporSiswaWizardViewModel.AnalisisBerkas semula
// "catch (Exception ex) { PesanGalat = ex.Message; }" -- menangkap dan
// menampilkan APA PUN, termasuk kegagalan internal yang seharusnya tidak
// tertelan diam-diam, dan pesan teknis mentah (bahasa Inggris, kadang
// path lengkap) untuk kegagalan berkas biasa. Pengujian ini membuktikan:
// kegagalan berkas yang dapat dipulihkan pengguna menghasilkan pesan
// Indonesia yang jelas dan tidak menyingkap detail teknis, sedangkan
// kegagalan internal tak terduga (di sini: koneksi database tertutup)
// TETAP MELEMPAR ke pemanggil -- tidak diubah jadi pesan ramah.
public sealed class ImporWizardKegagalanBerkasTests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    private static readonly string[] KolomBakuGuru = { "NAMA", "NIP", "PANGKAT", "JABATAN" };
    private static readonly string[] KolomBakuSiswa =
    {
        "NISN", "NAMA_LENGKAP", "JENIS_KELAMIN", "TEMPAT_LAHIR",
        "TANGGAL_LAHIR", "ALAMAT", "KELAS", "NAMA_ORANG_TUA", "NO_HP_ORANG_TUA",
    };

    // ========================= ImporGuruWizardViewModel =========================

    [Fact]
    public void ImporGuru_BerkasSah_AnalisisBerhasilTanpaPesanGalat()
    {
        using var database = new MigratedDatabase();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new ImporGuruWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu));
        var path = BuatBerkasGuru(("Guru Satu", "1234567890"));

        try
        {
            viewModel.AnalisisBerkas(path);

            Assert.Null(viewModel.PesanGalat);
            Assert.Equal(LangkahImpor.Preview, viewModel.LangkahSaatIni);
            Assert.Equal(1, viewModel.JumlahBaru);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImporGuru_BerkasTidakDitemukan_PesanJelasTanpaTeksMentah()
    {
        using var database = new MigratedDatabase();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new ImporGuruWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu));
        var pathTidakAda = Path.Combine(Path.GetTempPath(), $"guru-tidak-ada-{Guid.NewGuid():N}.xlsx");

        viewModel.AnalisisBerkas(pathTidakAda);

        Assert.Equal("Berkas tidak ditemukan. Pastikan lokasinya masih benar, lalu pilih berkas lagi.", viewModel.PesanGalat);
        Assert.DoesNotContain(pathTidakAda, viewModel.PesanGalat!);
        Assert.Null(viewModel.HasilImpor);
    }

    [Fact]
    public void ImporGuru_AksesDitolak_PesanJelasTanpaTeksMentah()
    {
        using var database = new MigratedDatabase();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new ImporGuruWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu));

        // Direktori berekstensi .xlsx: Windows menolak akses saat dibuka
        // sebagai berkas (UnauthorizedAccessException) -- diverifikasi
        // lewat percobaan nyata, bukan tebakan.
        var pathDirektori = Path.Combine(Path.GetTempPath(), $"guru-akses-ditolak-{Guid.NewGuid():N}.xlsx");
        Directory.CreateDirectory(pathDirektori);

        try
        {
            viewModel.AnalisisBerkas(pathDirektori);

            Assert.Equal("Berkas tidak bisa dibaca karena akses ditolak. Periksa izin berkas, lalu coba lagi.", viewModel.PesanGalat);
        }
        finally
        {
            Directory.Delete(pathDirektori);
        }
    }

    [Fact]
    public void ImporGuru_BerkasTerkunciProsesLain_PesanJelasTanpaTeksMentah()
    {
        using var database = new MigratedDatabase();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new ImporGuruWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu));
        var path = BuatBerkasGuru(("Guru Satu", "1234567890"));

        try
        {
            using var kunciEksklusif = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            viewModel.AnalisisBerkas(path);

            Assert.Equal("Berkas sedang dipakai aplikasi lain (mis. Excel). Tutup berkasnya, lalu coba lagi.", viewModel.PesanGalat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImporGuru_BerkasBukanExcelSah_PesanFormatJelas()
    {
        using var database = new MigratedDatabase();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new ImporGuruWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu));
        var path = Path.Combine(Path.GetTempPath(), $"guru-bukan-excel-{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(path, "ini bukan berkas excel, cuma teks biasa");

        try
        {
            viewModel.AnalisisBerkas(path);

            Assert.Equal("Berkas ini bukan berkas Excel (.xlsx) yang sah, atau isinya rusak. Periksa kembali berkasnya.", viewModel.PesanGalat);
        }
        finally
        {
            // ClosedXML tidak selalu melepas handle berkas saat konstruktor
            // XLWorkbook gagal karena konten rusak (using var tidak sempat
            // Dispose -- objeknya tidak pernah selesai dibuat). Di luar
            // cakupan A-8 untuk diperbaiki di kode produksi; cukup
            // dibersihkan sebaik mungkin di sini supaya test tidak flaky.
            HapusBerkasSebaikMungkin(path);
        }
    }

    [Fact]
    public void ImporGuru_HeaderKolomSalah_PesanValidasiAplikasiTetapTampil()
    {
        using var database = new MigratedDatabase();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new ImporGuruWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu));
        var path = Path.Combine(Path.GetTempPath(), $"guru-header-salah-{Guid.NewGuid():N}.xlsx");
        using (var workbook = new XLWorkbook())
        {
            workbook.Worksheets.Add("Guru").Cell(1, 1).SetValue("KOLOM_SALAH");
            workbook.SaveAs(path);
        }

        try
        {
            viewModel.AnalisisBerkas(path);

            Assert.Equal("Kolom 1 seharusnya 'NAMA', berkas ini punya 'KOLOM_SALAH'.", viewModel.PesanGalat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImporGuru_KegagalanInternalTakTerduga_TidakTertelanDiamDiam()
    {
        using var database = new MigratedDatabase();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        var viewModel = new ImporGuruWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu));
        var path = BuatBerkasGuru(("Guru Satu", "1234567890"));

        try
        {
            // Koneksi ditutup sebelum Analisis dipanggil -> baris valid
            // memicu query CariIdBerdasarkanNip pada koneksi tertutup ->
            // InvalidOperationException dari Microsoft.Data.Sqlite
            // (diverifikasi lewat percobaan nyata). Tipe ini SENGAJA tidak
            // termasuk daftar catch AnalisisBerkas -- harus tetap melempar,
            // bukan berubah jadi PesanGalat ramah.
            database.Connection.Close();

            Assert.Throws<InvalidOperationException>(() => viewModel.AnalisisBerkas(path));

            Assert.Null(viewModel.PesanGalat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ========================= ImporSiswaWizardViewModel =========================

    private static void SeedKelasSiswa(MigratedDatabase database)
    {
        database.Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat) VALUES (1, 1, '7A', 7);
            """);
        database.TambahAkun(1, "admin1", "admin", guruId: null);
    }

    [Fact]
    public void ImporSiswa_BerkasSah_AnalisisBerhasilTanpaPesanGalat()
    {
        using var database = new MigratedDatabase();
        SeedKelasSiswa(database);
        var viewModel = new ImporSiswaWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);
        var path = BuatBerkasSiswa(("0012345678", "Ahmad Fauzi"));

        try
        {
            viewModel.AnalisisBerkas(path);

            Assert.Null(viewModel.PesanGalat);
            Assert.Equal(LangkahImpor.Preview, viewModel.LangkahSaatIni);
            Assert.Equal(1, viewModel.JumlahBaru);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImporSiswa_BerkasTidakDitemukan_PesanJelasTanpaTeksMentah()
    {
        using var database = new MigratedDatabase();
        SeedKelasSiswa(database);
        var viewModel = new ImporSiswaWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);
        var pathTidakAda = Path.Combine(Path.GetTempPath(), $"siswa-tidak-ada-{Guid.NewGuid():N}.xlsx");

        viewModel.AnalisisBerkas(pathTidakAda);

        Assert.Equal("Berkas tidak ditemukan. Pastikan lokasinya masih benar, lalu pilih berkas lagi.", viewModel.PesanGalat);
        Assert.DoesNotContain(pathTidakAda, viewModel.PesanGalat!);
        Assert.Null(viewModel.HasilImpor);
    }

    [Fact]
    public void ImporSiswa_AksesDitolak_PesanJelasTanpaTeksMentah()
    {
        using var database = new MigratedDatabase();
        SeedKelasSiswa(database);
        var viewModel = new ImporSiswaWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);
        var pathDirektori = Path.Combine(Path.GetTempPath(), $"siswa-akses-ditolak-{Guid.NewGuid():N}.xlsx");
        Directory.CreateDirectory(pathDirektori);

        try
        {
            viewModel.AnalisisBerkas(pathDirektori);

            Assert.Equal("Berkas tidak bisa dibaca karena akses ditolak. Periksa izin berkas, lalu coba lagi.", viewModel.PesanGalat);
        }
        finally
        {
            Directory.Delete(pathDirektori);
        }
    }

    [Fact]
    public void ImporSiswa_BerkasTerkunciProsesLain_PesanJelasTanpaTeksMentah()
    {
        using var database = new MigratedDatabase();
        SeedKelasSiswa(database);
        var viewModel = new ImporSiswaWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);
        var path = BuatBerkasSiswa(("0012345678", "Ahmad Fauzi"));

        try
        {
            using var kunciEksklusif = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            viewModel.AnalisisBerkas(path);

            Assert.Equal("Berkas sedang dipakai aplikasi lain (mis. Excel). Tutup berkasnya, lalu coba lagi.", viewModel.PesanGalat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImporSiswa_BerkasBukanExcelSah_PesanFormatJelas()
    {
        using var database = new MigratedDatabase();
        SeedKelasSiswa(database);
        var viewModel = new ImporSiswaWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);
        var path = Path.Combine(Path.GetTempPath(), $"siswa-bukan-excel-{Guid.NewGuid():N}.xlsx");
        File.WriteAllText(path, "ini bukan berkas excel, cuma teks biasa");

        try
        {
            viewModel.AnalisisBerkas(path);

            Assert.Equal("Berkas ini bukan berkas Excel (.xlsx) yang sah, atau isinya rusak. Periksa kembali berkasnya.", viewModel.PesanGalat);
        }
        finally
        {
            // Lihat catatan di ImporGuru_BerkasBukanExcelSah_PesanFormatJelas.
            HapusBerkasSebaikMungkin(path);
        }
    }

    [Fact]
    public void ImporSiswa_HeaderKolomSalah_PesanValidasiAplikasiTetapTampil()
    {
        using var database = new MigratedDatabase();
        SeedKelasSiswa(database);
        var viewModel = new ImporSiswaWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);
        var path = Path.Combine(Path.GetTempPath(), $"siswa-header-salah-{Guid.NewGuid():N}.xlsx");
        using (var workbook = new XLWorkbook())
        {
            workbook.Worksheets.Add("Siswa").Cell(1, 1).SetValue("KOLOM_SALAH");
            workbook.SaveAs(path);
        }

        try
        {
            viewModel.AnalisisBerkas(path);

            Assert.Equal("Kolom 1 seharusnya 'NISN', berkas ini punya 'KOLOM_SALAH'.", viewModel.PesanGalat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ImporSiswa_KegagalanInternalTakTerduga_TidakTertelanDiamDiam()
    {
        using var database = new MigratedDatabase();
        SeedKelasSiswa(database);
        var viewModel = new ImporSiswaWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);
        var path = BuatBerkasSiswa(("0012345678", "Ahmad Fauzi"));

        try
        {
            // Koneksi ditutup sebelum Analisis dipanggil -> validasi KELAS
            // baris valid memicu query CariIdBerdasarkanNama pada koneksi
            // tertutup -> InvalidOperationException (Microsoft.Data.Sqlite,
            // diverifikasi lewat percobaan nyata). Tipe ini SENGAJA tidak
            // termasuk daftar catch AnalisisBerkas.
            database.Connection.Close();

            Assert.Throws<InvalidOperationException>(() => viewModel.AnalisisBerkas(path));

            Assert.Null(viewModel.PesanGalat);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Catatan soal cancellation (Bagian pengujian minimal #6): alur wizard
    // impor ini sepenuhnya sinkron, tidak memakai CancellationToken/Task
    // sama sekali. Satu-satunya bentuk "pembatalan" adalah membatalkan
    // OpenFileDialog (PilihBerkas), yang sudah sejak awal menangani lewat
    // "if (dialog.ShowDialog() != true) return;" SEBELUM AnalisisBerkas
    // sempat dipanggil -- tidak ada exception yang terlibat sama sekali di
    // jalur itu, jadi tidak ada perilaku untuk diuji ulang di sini.

    // ========================= Helper =========================

    private static void HapusBerkasSebaikMungkin(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort -- lihat catatan pemanggil.
        }
    }

    // ========================= Helper pembuat berkas =========================

    private static string BuatBerkasGuru(params (string Nama, string Nip)[] baris)
    {
        var path = Path.Combine(Path.GetTempPath(), $"impor-guru-app-uji-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Guru");

        for (var kolom = 0; kolom < KolomBakuGuru.Length; kolom++)
        {
            worksheet.Cell(1, kolom + 1).SetValue(KolomBakuGuru[kolom]);
        }

        for (var i = 0; i < baris.Length; i++)
        {
            var b = baris[i];
            var row = i + 2;
            worksheet.Cell(row, 1).SetValue(b.Nama);
            worksheet.Cell(row, 2).SetValue(b.Nip);
            worksheet.Cell(row, 3).SetValue("Pembina");
            worksheet.Cell(row, 4).SetValue("Guru Kelas");
        }

        workbook.SaveAs(path);
        return path;
    }

    private static string BuatBerkasSiswa(params (string Nisn, string Nama)[] baris)
    {
        var path = Path.Combine(Path.GetTempPath(), $"impor-siswa-app-uji-{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Siswa");

        for (var kolom = 0; kolom < KolomBakuSiswa.Length; kolom++)
        {
            worksheet.Cell(1, kolom + 1).SetValue(KolomBakuSiswa[kolom]);
        }

        for (var i = 0; i < baris.Length; i++)
        {
            var b = baris[i];
            var row = i + 2;
            worksheet.Cell(row, 1).SetValue(b.Nisn);
            worksheet.Cell(row, 2).SetValue(b.Nama);
            worksheet.Cell(row, 3).SetValue("L");
            worksheet.Cell(row, 4).SetValue("Makassar");
            worksheet.Cell(row, 5).SetValue("2013-01-01");
            worksheet.Cell(row, 6).SetValue("Jl. Contoh");
            worksheet.Cell(row, 7).SetValue("7A");
            worksheet.Cell(row, 8).SetValue("Budi");
            worksheet.Cell(row, 9).SetValue("081234567890");
        }

        workbook.SaveAs(path);
        return path;
    }
}

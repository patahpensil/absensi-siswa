using System.IO;
using Absensi.App;
using Absensi.App.MasterData;
using Absensi.Data.Akun;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using ClosedXML.Excel;

namespace Absensi.App.Tests;

// Koreksi Tugas 7 sesuai PRD §14.4-14.5, poin 1-5 (poin 6 — PengolahFoto —
// tidak diuji otomatis, murni pengolahan gambar).
public sealed class KoreksiTugas7Tests
{
    private static readonly DateTime Waktu = new(2026, 9, 17, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly string[] KolomBakuSiswa =
    {
        "NISN", "NAMA_LENGKAP", "JENIS_KELAMIN", "TEMPAT_LAHIR",
        "TANGGAL_LAHIR", "ALAMAT", "KELAS", "NAMA_ORANG_TUA", "NO_HP_ORANG_TUA",
    };

    private static void SeedDasar(MigratedDatabase database)
    {
        database.Execute("""
            INSERT INTO tahun_ajaran (id, nama, semester, tgl_mulai, tgl_selesai, aktif)
            VALUES (1, '2026/2027', 'ganjil', '2026-07-01', '2026-12-19', 1);

            INSERT INTO kelas (id, tahun_ajaran_id, nama, tingkat) VALUES (1, 1, '7A', 7);
            """);
        database.TambahAkun(1, "admin1", "admin", guruId: null);
    }

    // ---------- Poin 1: hitungan kelas terpilih ----------

    [Fact]
    public void SiswaViewModel_KelasDipilih_RingkasanMenunjukkanBelumKartuDanBelumFoto()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        database.Execute("""
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif, foto_path) VALUES (1, '0001', 'A Punya Semua', 'L', 1, 'a.png');
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif, foto_path) VALUES (2, '0002', 'B Tanpa Kartu', 'L', 1, 'b.png');
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (3, '0003', 'C Tanpa Apa-apa', 'L', 1);

            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (1, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (2, 1, 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (3, 1, 1);

            INSERT INTO kartu (uid, pemilik_tipe, pemilik_id, aktif, didaftar_pada)
            VALUES ('UID-1', 'siswa', 1, 1, '2026-09-01T00:00:00');
            """);

        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);

        Assert.Null(viewModel.RingkasanKelas);

        viewModel.KelasFilterTerpilih = viewModel.DaftarKelasFilter.Single(k => k.Nama == "7A");

        Assert.Equal("Kelas 7A · 2 siswa belum punya kartu · 1 belum berfoto", viewModel.RingkasanKelas);
    }

    // ---------- Poin 2: siswa tidak aktif tetap tampil, warna redup ----------

    [Fact]
    public void SiswaViewModel_SiswaDitandaiTidakAktif_TetapMunculDenganAlasan()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        database.Execute("""
            INSERT INTO siswa (id, nisn, nama, jenis_kelamin, aktif) VALUES (1, '0001', 'Siswa Pindah', 'L', 1);
            INSERT INTO siswa_kelas (siswa_id, kelas_id, tahun_ajaran_id) VALUES (1, 1, 1);
            """);

        new LayananSiswa().TandaiTidakAktif(database.Connection, Admin, Waktu, id: 1, alasan: "pindah");

        var viewModel = new SiswaViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), new StatusModeRegistrasi(), tahunAjaranId: 1);

        var baris = Assert.Single(viewModel.Daftar);
        Assert.False(baris.Aktif);
        Assert.Equal("pindah", baris.AlasanNonaktif);
        Assert.Equal("Siswa Pindah", baris.Nama);
    }

    // ---------- Poin 3: progres 4 langkah ----------

    [Fact]
    public void ImporSiswaWizard_ProgresLangkah_BerubahSesuaiTahapan()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = new ImporSiswaWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);
        Assert.Equal(LangkahImpor.Analisis, viewModel.LangkahSaatIni);

        var path = BuatBerkasSiswa(("0012345678", "Ahmad Fauzi"));
        try
        {
            viewModel.AnalisisBerkas(path);
            Assert.Equal(LangkahImpor.Preview, viewModel.LangkahSaatIni);

            viewModel.ImporCommand.Execute(null);
            Assert.Equal(LangkahImpor.Impor, viewModel.LangkahSaatIni);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---------- Poin 4: tiga angka pratinjau (dilengkapi poin 3) ----------

    [Fact]
    public void ImporSiswaWizard_SetelahAnalisis_TigaAngkaPratinjauTerisi()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = new ImporSiswaWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);

        var path = BuatBerkasSiswa(("0012345678", "Ahmad Fauzi"), ("123", "Siswa Rusak"));
        try
        {
            viewModel.AnalisisBerkas(path);

            Assert.Equal(1, viewModel.JumlahBaru);
            Assert.Equal(0, viewModel.JumlahDiperbarui);
            Assert.Equal(1, viewModel.JumlahBermasalah);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // ---------- Poin 5: tombol impor menyebut jumlah baris ----------

    [Fact]
    public void ImporSiswaWizard_TeksTombolImpor_MenyebutJumlahBarisSah()
    {
        using var database = new MigratedDatabase();
        SeedDasar(database);
        var viewModel = new ImporSiswaWizardViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), tahunAjaranId: 1);

        Assert.Equal("Impor", viewModel.TeksTombolImpor);

        var path = BuatBerkasSiswa(("0012345678", "Ahmad Fauzi"), ("0012345679", "Budi Santoso"), ("123", "Siswa Rusak"));
        try
        {
            viewModel.AnalisisBerkas(path);

            Assert.Equal("Impor 2 baris", viewModel.TeksTombolImpor);
        }
        finally
        {
            File.Delete(path);
        }
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

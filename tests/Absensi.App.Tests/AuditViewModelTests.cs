using System.IO;
using Absensi.App.Audit;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// Master Bug & UI/UX Register A-39 Tahap 2 + A-40. Logika penggabungan
// live+arsip sendiri sudah dibuktikan menyeluruh di LayananAuditTests
// (termasuk kriteria selesai A-39: restore berulang, tanpa duplikasi,
// nama pelaku benar) -- tes di sini fokus ke perilaku ViewModel: gerbang
// akses, rentang tanggal bawaan, dan penerjemahan hasil ke properti UI.
public sealed class AuditViewModelTests
{
    private static readonly DateTime Waktu = new(2026, 9, 25, 8, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);
    private static readonly AkunTersimpan Kepsek = new(2, "kepsek", "x", PeranAkun.KepalaSekolah, null, true);
    private static readonly AkunTersimpan WaliKelas = new(3, "wali_a", "x", PeranAkun.WaliKelas, 1, true);

    private static string FolderArsipSementara() => Path.Combine(Path.GetTempPath(), $"absensi-audit-vm-{Guid.NewGuid():N}");

    [Fact]
    public void BolehAkses_Admin_True_PeranLain_False()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();

        Assert.True(new AuditViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), FolderArsipSementara()).BolehAkses);
        Assert.False(new AuditViewModel(database.Connection, Kepsek, new PenyediaWaktuUji(Waktu), FolderArsipSementara()).BolehAkses);
        Assert.False(new AuditViewModel(database.Connection, WaliKelas, new PenyediaWaktuUji(Waktu), FolderArsipSementara()).BolehAkses);
    }

    [Theory]
    [MemberData(nameof(PeranBukanAdmin))]
    public void Konstruktor_BukanAdmin_PesanGalatDanBarisKosong_TidakMemanggilLayananAudit(AkunTersimpan bukanAdmin)
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();

        var viewModel = new AuditViewModel(database.Connection, bukanAdmin, new PenyediaWaktuUji(Waktu), FolderArsipSementara());

        Assert.NotNull(viewModel.PesanGalat);
        Assert.Empty(viewModel.Baris);
    }

    public static TheoryData<AkunTersimpan> PeranBukanAdmin => new() { Kepsek, WaliKelas };

    [Fact]
    public void Konstruktor_Admin_RentangBawaan90HariDanBarisTerisi()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        CatatAudit(database.Connection, 1, "perbarui_kelas", "kelas", Waktu.AddDays(-10));
        CatatAudit(database.Connection, 1, "perbarui_kelas", "kelas", Waktu.AddDays(-100)); // di luar 90 hari

        var viewModel = new AuditViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), FolderArsipSementara());

        Assert.Null(viewModel.PesanGalat);
        Assert.Equal(Waktu.Date.AddDays(-90), viewModel.DariTanggal);
        Assert.Single(viewModel.Baris); // hanya yang -10 hari, yang -100 hari di luar rentang bawaan
    }

    [Fact]
    public void MengubahDariTanggal_MemuatUlangOtomatis()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        CatatAudit(database.Connection, 1, "perbarui_kelas", "kelas", Waktu.AddDays(-100));

        var viewModel = new AuditViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), FolderArsipSementara());
        Assert.Empty(viewModel.Baris); // di luar rentang bawaan 90 hari

        viewModel.DariTanggal = Waktu.AddDays(-120);

        Assert.Single(viewModel.Baris); // sekarang masuk rentang setelah rentang diperluas
    }

    [Fact]
    public void MuatUlang_ArsipBermasalahDiterjemahkanKePesan()
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var waktuRestore = Waktu.AddDays(-5);
        CatatAudit(database.Connection, 1, "restore_backup", "sekolah", waktuRestore); // dirujuk tapi arsipnya sengaja tidak dibuat

        var viewModel = new AuditViewModel(database.Connection, Admin, new PenyediaWaktuUji(Waktu), FolderArsipSementara());

        Assert.True(viewModel.AdaArsipBermasalah);
        Assert.Single(viewModel.PesanArsipBermasalah);
        Assert.Contains("tidak dapat ditampilkan", viewModel.PesanArsipBermasalah[0]);
    }

    private static void CatatAudit(Microsoft.Data.Sqlite.SqliteConnection connection, int akunId, string aksi, string entitas, DateTime waktu)
    {
        using var transaksi = connection.BeginTransaction();
        new RepositoriAudit().Catat(connection, transaksi, waktu, akunId, aksi, entitas, null, null, "nilai-uji", null);
        transaksi.Commit();
    }
}

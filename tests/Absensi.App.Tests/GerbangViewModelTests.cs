using Absensi.App.Gerbang;

namespace Absensi.App.Tests;

// RENCANA-TUGAS.md Tugas 6, "Selesai bila": empat tap beruntun dalam satu
// detik tampil seluruhnya tanpa ada yang hilang.
public sealed class GerbangViewModelTests
{
    private static readonly DateTime Waktu0729 = new(2026, 9, 17, 7, 29, 0);

    private static GerbangViewModel BuatViewModel(MigratedDatabase database, PenyediaWaktuUji waktu, PemutarBunyiDiam? bunyi = null)
    {
        var cache = CacheTampilanGerbang.Muat(database.Connection);
        return new GerbangViewModel(database.Connection, waktu, bunyi ?? new PemutarBunyiDiam(), cache);
    }

    [Fact]
    public void ProsesTap_EmpatKaliDalamSatuDetik_SemuaTampilTanpaAdaYangHilang()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var waktu = new PenyediaWaktuUji(Waktu0729);
        var viewModel = BuatViewModel(database, waktu);

        viewModel.ProsesTap("UID-1");
        waktu.Sekarang = Waktu0729.AddMilliseconds(300);
        viewModel.ProsesTap("UID-2");
        waktu.Sekarang = Waktu0729.AddMilliseconds(600);
        viewModel.ProsesTap("UID-3");
        waktu.Sekarang = Waktu0729.AddMilliseconds(900);
        viewModel.ProsesTap("UID-4");

        Assert.Equal(4, viewModel.KartuAktif.Count);
        Assert.Empty(viewModel.Ringkasan);
        Assert.Equal(
            new[] { "Siswa Satu", "Siswa Dua", "Siswa Tiga", "Siswa Empat" },
            viewModel.KartuAktif.Select(k => k.Nama));
    }

    [Fact]
    public void ProsesTap_KelimaSaatEmpatSudahAktif_TertuaTurunKeRingkasanBukanHilang()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var waktu = new PenyediaWaktuUji(Waktu0729);
        var viewModel = BuatViewModel(database, waktu);

        viewModel.ProsesTap("UID-1");
        viewModel.ProsesTap("UID-2");
        viewModel.ProsesTap("UID-3");
        viewModel.ProsesTap("UID-4");
        viewModel.ProsesTap("UID-5");

        Assert.Equal(4, viewModel.KartuAktif.Count);
        Assert.Single(viewModel.Ringkasan);
        Assert.Equal("Siswa Satu", viewModel.Ringkasan[0].Nama);
        Assert.DoesNotContain(viewModel.KartuAktif, k => k.Nama == "Siswa Satu");
        Assert.Contains(viewModel.KartuAktif, k => k.Nama == "Siswa Lima");
    }

    [Fact]
    public void EvaluasiKartuKedaluwarsa_SetelahDelapanDetik_KartuTurunKeRingkasan()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var waktu = new PenyediaWaktuUji(Waktu0729);
        var viewModel = BuatViewModel(database, waktu);

        viewModel.ProsesTap("UID-1");
        waktu.Sekarang = Waktu0729.AddSeconds(8);
        viewModel.EvaluasiKartuKedaluwarsa();

        Assert.Empty(viewModel.KartuAktif);
        Assert.Single(viewModel.Ringkasan);
    }

    [Fact]
    public void EvaluasiKartuKedaluwarsa_SebelumDelapanDetik_KartuTetapAktif()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var waktu = new PenyediaWaktuUji(Waktu0729);
        var viewModel = BuatViewModel(database, waktu);

        viewModel.ProsesTap("UID-1");
        waktu.Sekarang = Waktu0729.AddSeconds(7);
        viewModel.EvaluasiKartuKedaluwarsa();

        Assert.Single(viewModel.KartuAktif);
        Assert.Empty(viewModel.Ringkasan);
    }

    [Fact]
    public void ProsesTap_KartuTidakTerdaftar_TampilKartuTidakDikenalDanBunyiGagal()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var waktu = new PenyediaWaktuUji(Waktu0729);
        var bunyi = new PemutarBunyiDiam();
        var viewModel = BuatViewModel(database, waktu, bunyi);

        viewModel.ProsesTap("UID-TIDAK-ADA");

        Assert.Equal("Kartu tidak dikenal", viewModel.KartuAktif[0].Nama);
        Assert.Equal(KategoriWarnaTap.Ditolak, viewModel.KartuAktif[0].KategoriWarna);
        Assert.Equal(1, bunyi.JumlahGagal);
        Assert.Equal(0, bunyi.JumlahBerhasil);
    }

    // Koreksi Tugas 6, poin 2: ringkasan harian (PRD §14.1) dihitung dari
    // RepositoriAbsensi dan diperbarui setiap tap masuk.

    [Fact]
    public void RingkasanHarian_SebelumAdaTap_BelumTapSamaDenganJumlahSiswa()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var waktu = new PenyediaWaktuUji(Waktu0729);
        var viewModel = BuatViewModel(database, waktu);

        Assert.Equal(5, viewModel.RingkasanBelumTap);
        Assert.Equal(0, viewModel.RingkasanHadir);
        Assert.Equal(0, viewModel.RingkasanTerlambat);
        Assert.Equal(0, viewModel.RingkasanGuru);
    }

    [Fact]
    public void ProsesTap_SiswaHadir_RingkasanHadirBertambahDanBelumTapBerkurang()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var waktu = new PenyediaWaktuUji(Waktu0729);
        var viewModel = BuatViewModel(database, waktu);

        viewModel.ProsesTap("UID-1");

        Assert.Equal(1, viewModel.RingkasanHadir);
        Assert.Equal(4, viewModel.RingkasanBelumTap);
    }

    [Fact]
    public void ProsesTap_SiswaTerlambat_RingkasanTerlambatBertambah()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var waktu = new PenyediaWaktuUji(new DateTime(2026, 9, 17, 7, 31, 0));
        var viewModel = BuatViewModel(database, waktu);

        viewModel.ProsesTap("UID-1");

        Assert.Equal(1, viewModel.RingkasanTerlambat);
        Assert.Equal(0, viewModel.RingkasanHadir);
    }

    [Fact]
    public void ProsesTap_GuruMasuk_RingkasanGuruBertambah()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var waktu = new PenyediaWaktuUji(Waktu0729);
        var viewModel = BuatViewModel(database, waktu);

        viewModel.ProsesTap("UID-GURU-1");

        Assert.Equal(1, viewModel.RingkasanGuru);
    }

    [Fact]
    public void ProsesTap_TapBerhasil_MemainkanBunyiBerhasil()
    {
        using var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        var waktu = new PenyediaWaktuUji(Waktu0729);
        var bunyi = new PemutarBunyiDiam();
        var viewModel = BuatViewModel(database, waktu, bunyi);

        viewModel.ProsesTap("UID-1");

        Assert.Equal("Hadir", viewModel.KartuAktif[0].TeksStatus);
        Assert.Equal(KategoriWarnaTap.Hadir, viewModel.KartuAktif[0].KategoriWarna);
        Assert.Equal(1, bunyi.JumlahBerhasil);
        Assert.Equal(0, bunyi.JumlahGagal);
    }
}

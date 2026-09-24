using System.IO;
using Absensi.App.Wizard;
using Absensi.Data;
using Absensi.Data.Backup;
using Absensi.Data.Lisensi;
using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.App.Tests;

// RENCANA-TUGAS.md Tugas 13 — PRD §16.1 (enam langkah) dan §18.3 (dua
// cabang eksplisit di langkah 1: Instalasi Baru vs Pulihkan dari PC Lama).
public sealed class WizardInstalasiViewModelTests : IDisposable
{
    private static readonly DateTime Waktu = new(2026, 9, 18, 8, 0, 0);
    private readonly string _folderCloudStub = Path.Combine(Path.GetTempPath(), $"absensi-wizard-cloud-{Guid.NewGuid():N}");

    private WizardInstalasiViewModel BuatViewModel(MigratedDatabase database) => new(
        database.Connection, new PenyediaWaktuUji(Waktu), new LayananLisensiLokal(), new PenyimpanCloudBackupLokal(_folderCloudStub));

    [Fact]
    public void Konstruksi_LangkahAwalAktivasiLisensiDanTujuhBarisJadwal()
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);

        Assert.Equal(LangkahWizard.AktivasiLisensi, viewModel.LangkahSaatIni);
        Assert.Equal(7, viewModel.JadwalHarian.Count);
        Assert.False(viewModel.JadwalHarian[6].HariSekolah);
    }

    // Bug jadwal kosong (docs/MASTER-BUG-UIUX-REGISTER.md, analisis
    // penolakan Gerbang): sebelumnya keempat kolom jam diisi null/kosong di
    // konstruktor, layar tidak menampilkan nilai bawaan PRD §7 apa pun —
    // operator mengosongkan seluruh baris termasuk hari sekolah. Sekarang
    // nilai bawaan PRD §7 benar-benar terisi sejak baris ditampilkan.
    [Fact]
    public void Konstruksi_JadwalHarianTerisiNilaiBawaanPrdBukanKosong()
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);

        // Senin, Selasa, Rabu, Kamis, Sabtu (indeks 0,1,2,3,5) — PRD §7.
        foreach (var indeks in new[] { 0, 1, 2, 3, 5 })
        {
            var baris = viewModel.JadwalHarian[indeks];
            Assert.Equal("07:30", baris.JamMasuk);
            Assert.Equal("08:00", baris.JamRekap);
            Assert.Equal("12:00", baris.BatasTapMasuk);
            Assert.Equal("13:10", baris.MulaiPulang);
        }

        var jumat = viewModel.JadwalHarian[4];
        Assert.Equal("07:30", jumat.JamMasuk);
        Assert.Equal("08:00", jumat.JamRekap);
        Assert.Equal("10:00", jumat.BatasTapMasuk);
        Assert.Equal("11:30", jumat.MulaiPulang);

        // Minggu tetap kosong — bukan hari sekolah, PRD §7 sendiri tidak
        // punya nilai untuk baris itu (teks instruksi layar mengizinkan
        // mengosongkannya).
        var minggu = viewModel.JadwalHarian[6];
        Assert.Null(minggu.JamMasuk);
        Assert.Null(minggu.JamRekap);
        Assert.Null(minggu.BatasTapMasuk);
        Assert.Null(minggu.MulaiPulang);
    }

    [Fact]
    public void KunciLisensiKosong_KeduaCabangTidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);

        Assert.False(viewModel.InstalasiBaruCommand.CanExecute(null));
        Assert.False(viewModel.PulihkanDariPcLamaCommand.CanExecute(null));

        viewModel.KunciLisensi = "SEKOLAH-BARU-001";

        Assert.True(viewModel.InstalasiBaruCommand.CanExecute(null));
        Assert.True(viewModel.PulihkanDariPcLamaCommand.CanExecute(null));
    }

    // PRD §18.3 cabang "Instalasi Baru": aktifkan lisensi lalu lanjut ke
    // langkah 2, terlepas dari ada-tidaknya backup cloud untuk kunci itu.
    [Fact]
    public void InstalasiBaruCommand_KunciDiisi_PindahKeLangkahIdentitasSekolah()
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);
        viewModel.KunciLisensi = "SEKOLAH-BARU-001";

        viewModel.InstalasiBaruCommand.Execute(null);

        Assert.Equal(LangkahWizard.IdentitasSekolah, viewModel.LangkahSaatIni);
        Assert.Null(viewModel.PesanGalat);
    }

    [Fact]
    public void LanjutKeTahunAjaranCommand_NamaSekolahKosong_TidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);

        Assert.False(viewModel.LanjutKeTahunAjaranCommand.CanExecute(null));

        viewModel.NamaSekolah = "SMP Negeri Uji";
        Assert.True(viewModel.LanjutKeTahunAjaranCommand.CanExecute(null));
    }

    private static void MajukanSampaiAkun(WizardInstalasiViewModel viewModel)
    {
        viewModel.KunciLisensi = "SEKOLAH-BARU-001";
        viewModel.InstalasiBaruCommand.Execute(null);

        viewModel.NamaSekolah = "SMP Negeri Uji";
        viewModel.LanjutKeTahunAjaranCommand.Execute(null);

        viewModel.NamaTahunAjaran = "2026/2027";
        viewModel.TglMulaiTahunAjaran = new DateTime(2026, 7, 1);
        viewModel.TglSelesaiTahunAjaran = new DateTime(2026, 12, 19);
        viewModel.LanjutKeJadwalCommand.Execute(null);

        viewModel.LanjutKeAkunCommand.Execute(null);
    }

    [Fact]
    public void SelesaikanCommand_SandiTidakCocok_TidakBisaDieksekusi()
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);
        MajukanSampaiAkun(viewModel);

        viewModel.UsernameKepalaSekolah = "kepsek";
        viewModel.SandiKepalaSekolah = "Rahasia123!";
        viewModel.KonfirmasiSandi = "Beda!";

        Assert.False(viewModel.SelesaikanCommand.CanExecute(null));
    }

    [Fact]
    public void Selesaikan_AlurLengkap_MenulisSemuaDanPindahKeLangkahSelesai()
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);
        MajukanSampaiAkun(viewModel);

        viewModel.UsernameKepalaSekolah = "kepsek";
        viewModel.SandiKepalaSekolah = "Rahasia123!";
        viewModel.KonfirmasiSandi = "Rahasia123!";

        AkunTersimpan? akunDariEvent = null;
        viewModel.WizardSelesai += (_, akun) => akunDariEvent = akun;

        viewModel.SelesaikanCommand.Execute(null);

        Assert.Equal(LangkahWizard.Selesai, viewModel.LangkahSaatIni);
        Assert.NotNull(akunDariEvent);
        Assert.Equal("kepsek", akunDariEvent!.Username);

        var sekolah = new RepositoriSekolah().CariSatuSatunya(database.Connection);
        Assert.NotNull(sekolah);
        Assert.Equal("SMP Negeri Uji", sekolah!.Nama);

        var status = new RepositoriLisensi().BacaStatus(database.Connection);
        Assert.True(status!.Aktif);
    }

    [Fact]
    public void Selesaikan_FormatJamTidakValid_KembaliKeLangkahJadwalTanpaMenulisApaPun()
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);
        MajukanSampaiAkun(viewModel);
        viewModel.JadwalHarian[0].JamMasuk = "bukan-jam";

        viewModel.UsernameKepalaSekolah = "kepsek";
        viewModel.SandiKepalaSekolah = "Rahasia123!";
        viewModel.KonfirmasiSandi = "Rahasia123!";

        viewModel.SelesaikanCommand.Execute(null);

        Assert.Equal(LangkahWizard.JadwalHarian, viewModel.LangkahSaatIni);
        Assert.Contains("Senin", viewModel.PesanGalat);
        Assert.Null(new RepositoriSekolah().CariSatuSatunya(database.Connection));
    }

    // Inti perbaikan Bug #11: hari sekolah dengan jam dikosongkan (dulu
    // diterima diam-diam, jadi akar masalah jadwal_hari NULL di UAT) wajib
    // ditolak Selesaikan dengan pesan jelas — bukan tersimpan sebagai NULL.
    [Fact]
    public void Selesaikan_HariSekolahJamMasukDikosongkan_DitolakTanpaMenulisApaPun()
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);
        MajukanSampaiAkun(viewModel);
        viewModel.JadwalHarian[0].JamMasuk = "   "; // Senin — hari sekolah bawaan

        viewModel.UsernameKepalaSekolah = "kepsek";
        viewModel.SandiKepalaSekolah = "Rahasia123!";
        viewModel.KonfirmasiSandi = "Rahasia123!";

        viewModel.SelesaikanCommand.Execute(null);

        Assert.Equal(LangkahWizard.JadwalHarian, viewModel.LangkahSaatIni);
        Assert.Contains("Senin", viewModel.PesanGalat);
        Assert.Contains("wajib diisi", viewModel.PesanGalat);
        Assert.Null(new RepositoriSekolah().CariSatuSatunya(database.Connection));
    }

    // Uncentang "Hari sekolah" tetap boleh mengosongkan jam — validasi
    // wajib mengikuti checkbox baris itu sendiri, bukan daftar hari
    // hardcoded, supaya kalender per-sekolah yang berbeda dari bawaan PRD
    // §7 (mis. libur Sabtu) tidak ikut terhalang.
    [Fact]
    public void Selesaikan_HariSekolahDiuncentangJamDikosongkan_TetapDiterima()
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);
        MajukanSampaiAkun(viewModel);
        viewModel.JadwalHarian[0].HariSekolah = false;
        viewModel.JadwalHarian[0].JamMasuk = null;
        viewModel.JadwalHarian[0].JamRekap = null;
        viewModel.JadwalHarian[0].BatasTapMasuk = null;
        viewModel.JadwalHarian[0].MulaiPulang = null;

        viewModel.UsernameKepalaSekolah = "kepsek";
        viewModel.SandiKepalaSekolah = "Rahasia123!";
        viewModel.KonfirmasiSandi = "Rahasia123!";

        viewModel.SelesaikanCommand.Execute(null);

        Assert.Equal(LangkahWizard.Selesai, viewModel.LangkahSaatIni);
    }

    // "Pastikan jadwal bertahan setelah aplikasi dibuka ulang": tutup
    // koneksi sepenuhnya (pola sama seperti durabilitas Simpan di
    // MasterDataViewModelTests) dan baca ulang lewat koneksi BARU ke
    // berkas yang sama — bukan lewat koneksi yang masih dipegang test.
    [Fact]
    public void Selesaikan_JadwalBawaanTidakDiubah_TersimpanBenarDanBertahanSetelahDibukaUlang()
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);
        MajukanSampaiAkun(viewModel);

        viewModel.UsernameKepalaSekolah = "kepsek";
        viewModel.SandiKepalaSekolah = "Rahasia123!";
        viewModel.KonfirmasiSandi = "Rahasia123!";

        viewModel.SelesaikanCommand.Execute(null);
        Assert.Equal(LangkahWizard.Selesai, viewModel.LangkahSaatIni);

        var tahunAjaranId = new RepositoriTahunAjaran().CariIdAktif(database.Connection);
        Assert.NotNull(tahunAjaranId);

        var pathBerkas = database.PathBerkas;
        database.Connection.Dispose();
        SqliteConnection.ClearAllPools();

        using var koneksiBaru = SqliteConnectionFactory.Open(pathBerkas);
        var repositoriJadwal = new RepositoriJadwalHari();

        var senin = repositoriJadwal.CariUntukHari(koneksiBaru, tahunAjaranId!.Value, 1);
        Assert.NotNull(senin);
        Assert.Equal(new TimeOnly(7, 30), senin!.JamMasuk);
        Assert.Equal(new TimeOnly(8, 0), senin.JamRekap);
        Assert.Equal(new TimeOnly(12, 0), senin.BatasTapMasuk);
        Assert.Equal(new TimeOnly(13, 10), senin.MulaiPulang);

        var jumat = repositoriJadwal.CariUntukHari(koneksiBaru, tahunAjaranId.Value, 5);
        Assert.NotNull(jumat);
        Assert.Equal(new TimeOnly(10, 0), jumat!.BatasTapMasuk);
        Assert.Equal(new TimeOnly(11, 30), jumat.MulaiPulang);

        // Minggu tetap NULL sesuai PRD §7 — bukan bug, sengaja tidak
        // punya jam karena bukan hari sekolah.
        var minggu = repositoriJadwal.CariUntukHari(koneksiBaru, tahunAjaranId.Value, 7);
        Assert.NotNull(minggu);
        Assert.Null(minggu!.JamMasuk);
        Assert.Null(minggu.MulaiPulang);
    }

    // PRD §18.3 cabang "Pulihkan dari PC Lama": aktifkan lisensi, tarik
    // backup terakhir dari cloud, lalu skip langkah 2-5 sepenuhnya.
    [Fact]
    public async Task PulihkanDariPcLamaCommand_AdaBackupCloudUntukKunciItu_MemicuEventDenganDataAsli()
    {
        var penyimpanCloud = new PenyimpanCloudBackupLokal(_folderCloudStub);
        var dataAsli = "isi database sekolah lama"u8.ToArray();
        var terenkripsi = EnkripsiBackup.Enkripsi(dataAsli, "SEKOLAH-LAMA-001");
        await penyimpanCloud.UnggahAsync("SEKOLAH-LAMA-001", terenkripsi, new DateTime(2026, 9, 1, 6, 0, 0));

        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);
        viewModel.KunciLisensi = "SEKOLAH-LAMA-001";

        byte[]? dataDariEvent = null;
        viewModel.DipulihkanDariCloud += (_, data) => dataDariEvent = data;
        viewModel.PulihkanDariPcLamaCommand.Execute(null);

        Assert.Equal(dataAsli, dataDariEvent);
        Assert.Null(viewModel.PesanGalat);
        // Cabang ini tidak pernah pindah ke langkah 2 — langsung selesai
        // lewat event DipulihkanDariCloud, ditangani App.xaml.cs.
        Assert.Equal(LangkahWizard.AktivasiLisensi, viewModel.LangkahSaatIni);
    }

    [Fact]
    public void PulihkanDariPcLamaCommand_TidakAdaBackupUntukKunciItu_PesanGalatMenyebutInstalasiBaru()
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);
        viewModel.KunciLisensi = "SEKOLAH-BARU-TANPA-BACKUP";

        byte[]? dataDariEvent = null;
        viewModel.DipulihkanDariCloud += (_, data) => dataDariEvent = data;
        viewModel.PulihkanDariPcLamaCommand.Execute(null);

        Assert.Null(dataDariEvent);
        Assert.Contains("Instalasi Baru", viewModel.PesanGalat);
    }

    // Koreksi istilah (2026-09-25): penyimpanan di balik "Pulihkan dari PC
    // Lama" saat ini masih folder lokal (PenyimpanCloudBackupLokal), BUKAN
    // server di luar PC -- pesan ke pengguna tidak boleh menyebut "cloud"
    // atau menyiratkan perlindungan dari kehilangan PC.
    [Fact]
    public void PulihkanDariPcLamaCommand_TidakAdaBackup_PesanGalatTidakMenyebutCloud()
    {
        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);
        viewModel.KunciLisensi = "SEKOLAH-BARU-TANPA-BACKUP";

        viewModel.PulihkanDariPcLamaCommand.Execute(null);

        Assert.DoesNotContain("cloud", viewModel.PesanGalat, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cadangan sekunder", viewModel.PesanGalat);
    }

    // Berkas cloud rusak/dienkripsi dengan kunci lain tidak boleh
    // melemparkan galat mentah — diperlakukan sama seperti tidak ada backup.
    [Fact]
    public async Task PulihkanDariPcLamaCommand_BackupCloudRusakTidakBisaDidekripsi_DiperlakukanSepertiTidakAda()
    {
        var penyimpanCloud = new PenyimpanCloudBackupLokal(_folderCloudStub);
        var terenkripsiDenganKunciLain = EnkripsiBackup.Enkripsi("data"u8.ToArray(), "KUNCI-LAIN");
        await penyimpanCloud.UnggahAsync("SEKOLAH-BARU-001", terenkripsiDenganKunciLain, DateTime.UtcNow);

        using var database = new MigratedDatabase();
        var viewModel = BuatViewModel(database);
        viewModel.KunciLisensi = "SEKOLAH-BARU-001";

        byte[]? dataDariEvent = null;
        viewModel.DipulihkanDariCloud += (_, data) => dataDariEvent = data;
        var pengecualian = Record.Exception(() => viewModel.PulihkanDariPcLamaCommand.Execute(null));

        Assert.Null(pengecualian);
        Assert.Null(dataDariEvent);
        Assert.Contains("Instalasi Baru", viewModel.PesanGalat);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folderCloudStub))
        {
            Directory.Delete(_folderCloudStub, recursive: true);
        }
    }
}

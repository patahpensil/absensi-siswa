using System.IO;
using Absensi.App.Wizard;
using Absensi.Data.Backup;
using Absensi.Data.Lisensi;
using Absensi.Data.Repositori;

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

using System.Reflection;
using System.Windows;
using Absensi.App.Gerbang;
using Absensi.App.Login;
using Absensi.App.Shell;
using Absensi.Data;
using Absensi.Data.Migrations;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Microsoft.Data.Sqlite;

namespace Absensi.App;

public partial class App : Application
{
    private readonly IPenyediaWaktu _penyediaWaktu = new PenyediaWaktuSistem();
    private SqliteConnection? _connection;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        LokasiData.PastikanFolderDataAda();
        _connection = SqliteConnectionFactory.Open(LokasiData.PathDatabase);
        new MigrationRunner().Run(_connection);
        JalankanPembukaanHariBilaSudahDikonfigurasi();

        var nomorVersi = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
        var sekolah = new RepositoriSekolah().CariSatuSatunya(_connection);

        var loginViewModel = new LoginViewModel(_connection, nomorVersi, sekolah?.Nama);
        var loginWindow = new LoginWindow(loginViewModel);

        if (loginWindow.ShowDialog() != true || loginWindow.AkunLogin is null)
        {
            Shutdown();
            return;
        }

        var tahunAjaranAktif = new RepositoriTahunAjaran().CariNamaAktif(_connection);
        var mainWindowViewModel = new MainWindowViewModel(sekolah?.Nama ?? "", sekolah?.LogoPath, tahunAjaranAktif);
        var mainWindow = new MainWindow(mainWindowViewModel, BukaGerbang);

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    // "Muat seluruh foto ke memori saat layar dibuka" (RENCANA-TUGAS.md
    // Tugas 6) — cache (termasuk nama sekolah + logo, PRD §14.1) dimuat
    // ulang setiap layar gerbang dibuka, bukan sekali saat aplikasi start,
    // supaya selalu mengikuti data siswa/guru aktif terbaru.
    private Window BukaGerbang()
    {
        var cache = CacheTampilanGerbang.Muat(_connection!);
        var gerbangViewModel = new GerbangViewModel(_connection!, _penyediaWaktu, new PemutarBunyiSistem(), cache);
        return new GerbangWindow(gerbangViewModel, _connection!);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _connection?.Dispose();
        base.OnExit(e);
    }

    // PRD §16.7 & RENCANA-TUGAS.md Tugas 3: pembukaan hari berjalan "saat
    // aplikasi dibuka". Sebelum wizard instalasi pertama (Tugas 13) ada,
    // instalasi baru belum punya tahun ajaran aktif — keadaan itu wajar,
    // bukan galat, jadi dilewati saja tanpa menjalankan pembukaan hari.
    private void JalankanPembukaanHariBilaSudahDikonfigurasi()
    {
        if (new RepositoriTahunAjaran().CariIdAktif(_connection!) is null)
        {
            return;
        }

        var hariIni = DateOnly.FromDateTime(_penyediaWaktu.Sekarang);
        new PembukaanHari().Jalankan(_connection!, hariIni);
    }
}

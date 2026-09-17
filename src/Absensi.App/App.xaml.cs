using System.Reflection;
using System.Windows;
using Absensi.App.Gerbang;
using Absensi.App.Login;
using Absensi.App.RegistrasiKartu;
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
    private readonly StatusModeRegistrasi _statusModeRegistrasi = new();
    private SqliteConnection? _connection;
    private AkunTersimpan? _akunLogin;
    private int? _tahunAjaranId;

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

        _akunLogin = loginWindow.AkunLogin;

        var repositoriTahunAjaran = new RepositoriTahunAjaran();
        _tahunAjaranId = repositoriTahunAjaran.CariIdAktif(_connection);
        var tahunAjaranAktif = repositoriTahunAjaran.CariNamaAktif(_connection);
        var mainWindowViewModel = new MainWindowViewModel(
            _connection, _akunLogin, _penyediaWaktu, _statusModeRegistrasi,
            sekolah?.Nama ?? "", sekolah?.LogoPath, tahunAjaranAktif, _tahunAjaranId);
        var mainWindow = new MainWindow(mainWindowViewModel, BukaGerbang, BukaRegistrasiKartu);

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
        var gerbangViewModel = new GerbangViewModel(_connection!, _penyediaWaktu, new PemutarBunyiSistem(), cache, _statusModeRegistrasi);
        return new GerbangWindow(gerbangViewModel, _connection!);
    }

    // RENCANA-TUGAS.md Tugas 8 — PRD §14.3 tombol "Registrasi". Tombol
    // "Buka layar gerbang"/"Registrasi" tampak begitu menu Absensi dipilih
    // walau tahun ajaran aktif belum diatur (celah yang sama sudah ada
    // sejak Tugas 6 untuk gerbang) — dijaga di sini dengan galat yang
    // jelas, sama seperti PemrosesTap, bukan NullReferenceException mentah.
    private Window BukaRegistrasiKartu()
    {
        if (_tahunAjaranId is not { } tahunAjaranId)
        {
            throw new InvalidOperationException("Tidak ada tahun ajaran aktif; Registrasi Kartu tidak bisa dibuka.");
        }

        var viewModel = new RegistrasiKartuViewModel(_connection!, _akunLogin!, _penyediaWaktu, _statusModeRegistrasi, tahunAjaranId);
        return new RegistrasiKartuWindow(viewModel);
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

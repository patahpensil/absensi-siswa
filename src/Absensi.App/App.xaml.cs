using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Absensi.App.Gerbang;
using Absensi.App.Login;
using Absensi.App.RegistrasiKartu;
using Absensi.App.Shell;
using Absensi.App.Wizard;
using Absensi.Data;
using Absensi.Data.Backup;
using Absensi.Data.Diagnostik;
using Absensi.Data.Lisensi;
using Absensi.Data.Migrations;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Telegram;
using Microsoft.Data.Sqlite;

namespace Absensi.App;

// Tugas 14 (pengerasan): App.xaml mengatur ShutdownMode="OnExplicitShutdown"
// — WPF TIDAK LAGI mematikan aplikasi otomatis begitu jendela terakhir
// tertutup. Sebelum ini, wizard instalasi pertama (Tugas 13) menabrak
// bawaan WPF ShutdownMode="OnLastWindowClose": begitu jendela wizard
// tertutup, WPF mendeteksi nol jendela terbuka dan langsung mematikan
// Application SEBELUM baris kode di bawah sempat membuka LoginWindow —
// aplikasi keluar diam-diam tanpa pernah menampilkan layar Masuk. Celah ini
// tidak pernah muncul sebelum Tugas 13 karena LoginWindow dulu selalu jadi
// jendela PERTAMA yang dibuka (tidak pernah ada momen "nol jendela terbuka"
// di tengah OnStartup). Konsekuensinya sekarang: SETIAP titik yang memang
// harus mengakhiri aplikasi wajib memanggil Shutdown() secara eksplisit
// (lihat titik-titik itu di bawah dan di MainWindow.xaml.cs) — tidak ada
// lagi jaring pengaman otomatis dari WPF.
public partial class App : Application
{
    private readonly IPenyediaWaktu _penyediaWaktu = new PenyediaWaktuSistem();
    private readonly StatusModeRegistrasi _statusModeRegistrasi = new();
    private SqliteConnection? _connection;
    private AkunTersimpan? _akunLogin;
    private int? _tahunAjaranId;
    private Penjadwal? _penjadwal;
    private DispatcherTimer? _penjadwalTimer;
    private LayananBackup? _layananBackup;

    // docs/INVESTIGASI-CRASH-2026-09-21.md §4.3 langkah 4: mencegah dialog
    // galat bertumpuk/rekursi kalau MessageBox.Show di
    // DispatcherUnhandledException sendiri memicu exception baru yang
    // terdispatch ulang ke handler yang sama.
    private bool _sedangMenanganiGalatFatal;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // docs/INVESTIGASI-CRASH-2026-09-21.md §4.2/§4.3: didaftarkan
        // paling awal, sebelum baris apa pun yang bisa melempar (termasuk
        // membuka koneksi database di bawah ini), supaya exception sedini
        // mungkin pun tertangkap dan tercatat.
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += App_AppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += App_UnobservedTaskException;

        // UAT Bug #11 (2026-09-23, diagnosis putaran keempat): kegagalan
        // binding WPF (mis. path binding salah, konverter melempar) TIDAK
        // pernah muncul sebagai exception .NET — WPF hanya menulis ke
        // PresentationTraceSources dan, secara default, TIDAK ADA listener
        // yang membaca itu sama sekali di build Release (beda dari Debug
        // di Visual Studio yang punya Output Window). Titik ini sama
        // sekali tidak tertangkap oleh 3 handler global di atas maupun
        // oleh PencatatGalat manapun sebelum sesi ini. Diarahkan ke
        // PencatatGalat (folder logs/ yang sama) supaya kegagalan binding
        // di layar mana pun (bukan cuma Registrasi Kartu) akhirnya
        // terlihat di build Release — bukan instrumentasi sekali pakai,
        // dipertahankan permanen.
        System.Diagnostics.PresentationTraceSources.Refresh();
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(new PencatatGalatTraceListener());
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Warning;

        LokasiData.PastikanFolderDataAda();
        LokasiData.PastikanFolderLogAda();
        _connection = SqliteConnectionFactory.Open(LokasiData.PathDatabase);
        new MigrationRunner().Run(_connection);

        // PRD §16.1: wizard hanya berjalan sekali, dideteksi lewat
        // ADA-tidaknya baris `sekolah` (RepositoriSekolah — lihat catatan
        // di sana). Tidak bisa dijalankan ulang setelah baris itu ada.
        var sekolah = new RepositoriSekolah().CariSatuSatunya(_connection);
        if (sekolah is null)
        {
            sekolah = JalankanWizardInstalasi();
            if (sekolah is null)
            {
                Shutdown();
                return;
            }
        }

        _layananBackup = new LayananBackup(LokasiData.FolderBackup, new PenyimpanCloudBackupLokal(LokasiData.FolderBackupCloudStub));
        JalankanBackupPembukaanAplikasi();

        JalankanPembukaanHariBilaSudahDikonfigurasi();

        var nomorVersi = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
        var statusLisensi = new RepositoriLisensi().BacaStatus(_connection);

        var loginViewModel = new LoginViewModel(_connection, nomorVersi, sekolah.Nama, sekolah.LogoPath, statusLisensi);
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
            sekolah.Nama, sekolah.LogoPath, tahunAjaranAktif, _tahunAjaranId, _layananBackup!);
        var mainWindow = new MainWindow(mainWindowViewModel, BukaGerbang, BukaRegistrasiKartu, SelesaikanRestoreDanTutup);

        MainWindow = mainWindow;
        mainWindow.Show();

        MulaiPenjadwal();
    }

    // RENCANA-TUGAS.md Tugas 10 — rekap 08.00/batas akhir tap/finalisasi
    // Bolos (PRD §9.2, jendela toleransi §9.4), plus memutar pengirim
    // outbox dan penerima pairing Telegram. 30 detik dipilih supaya
    // jendela toleransi (bawaan 15 menit) selalu tertangkap tanpa
    // membebani — bukan nilai dari PRD.
    private void MulaiPenjadwal()
    {
        _penjadwal = new Penjadwal(_connection!, _penyediaWaktu, new KlienBotTelegram(), _layananBackup!);
        _penjadwalTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _penjadwalTimer.Tick += async (_, _) =>
        {
            // Satu detak yang gagal (mis. tahun ajaran belum diatur, galat
            // jaringan sesaat) tidak boleh mematikan aplikasi absensi
            // gerbang yang sedang dipakai — dicoba lagi detak berikutnya,
            // bukan ditelan diam-diam selamanya.
            try
            {
                await _penjadwal.DetakAsync();
            }
            catch (Exception galat)
            {
                PencatatGalat.Catat(galat, sumber: "Penjadwal.DetakAsync", level: "ERROR");
            }
        };
        _penjadwalTimer.Start();
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
    // sejak Tugas 6 untuk gerbang).
    //
    // UAT Bug #11 (2026-09-22): versi sebelumnya MELEMPAR
    // InvalidOperationException di sini alih-alih menampilkan pesan —
    // exception itu tidak tertangkap di mana pun sampai
    // DispatcherUnhandledException, yang menampilkan dialog GENERIK
    // "aplikasi mengalami galat tak terduga dan perlu ditutup" (bukan
    // pesan "tidak ada tahun ajaran aktif" yang dimaksud) DAN membiarkan
    // proses berhenti (e.Handled tidak pernah diubah jadi true — lihat
    // App_DispatcherUnhandledException). Klik "Registrasi" tanpa tahun
    // ajaran aktif sebelumnya MENUTUP SELURUH APLIKASI. Diperbaiki dengan
    // menampilkan pesan yang jelas di sini dan TIDAK membuka jendela sama
    // sekali (mengembalikan null), bukan melempar exception.
    private Window? BukaRegistrasiKartu()
    {
        if (_tahunAjaranId is not { } tahunAjaranId)
        {
            MessageBox.Show(
                "Tidak ada tahun ajaran aktif. Atur tahun ajaran terlebih dahulu (menu Pengaturan) sebelum membuka Registrasi Kartu.",
                "Absensi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }

        var viewModel = new RegistrasiKartuViewModel(_connection!, _akunLogin!, _penyediaWaktu, _statusModeRegistrasi, tahunAjaranId);
        return new RegistrasiKartuWindow(viewModel);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= App_AppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= App_UnobservedTaskException;

        _penjadwalTimer?.Stop();
        _connection?.Dispose();
        base.OnExit(e);
    }

    // docs/INVESTIGASI-CRASH-2026-09-21.md §4.3: jaring pengaman terakhir
    // untuk exception yang lolos dari SEMUA command/event handler UI thread
    // (mis. dugaan §2.1 — GerbangViewModel.ProsesTap yang berjalan sinkron
    // di dalam PreviewKeyDown tanpa try/catch). Whitelist pemulihan SENGAJA
    // kosong pada rilis pertama (§4.4) — TIDAK ADA cabang di sini yang
    // menyatakan sebuah jenis exception "aman dilanjutkan" tanpa bukti per
    // kasus, jadi e.Handled TIDAK PERNAH diubah jadi true oleh kode ini.
    // Exception dibiarkan lanjut ke jalur default .NET (proses berhenti,
    // Windows tetap mencatat APPCRASH seperti sebelumnya) — bedanya
    // sekarang sudah ada baris log kita sendiri DENGAN stack trace sebelum
    // proses benar-benar berhenti.
    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        PencatatGalat.Catat(e.Exception, sumber: "DispatcherUnhandledException", level: "FATAL");

        if (_sedangMenanganiGalatFatal)
        {
            // Handler ini terpanggil lagi selagi exception SEBELUMNYA masih
            // diproses (mis. MessageBox.Show di bawah sendiri memicu
            // exception baru yang terdispatch ulang ke sini) — jangan
            // tampilkan dialog kedua, exception baru ini tetap sudah
            // tercatat oleh baris di atas.
            return;
        }

        _sedangMenanganiGalatFatal = true;
        try
        {
            MessageBox.Show(
                "Aplikasi mengalami galat tak terduga dan perlu ditutup. Rincian teknis sudah dicatat di folder 'logs'. Buka kembali aplikasi untuk melanjutkan.",
                "Absensi", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // Exception aslinya sudah tercatat di atas — kegagalan
            // menampilkan dialog tidak boleh menggantikan/menutupi
            // penanganan exception yang sebenarnya.
        }
    }

    // docs/INVESTIGASI-CRASH-2026-09-21.md §4.3: best-effort logging SAJA.
    // TIDAK ADA MessageBox, operasi database, atau pemicuan backup di sini
    // — proses ada kemungkinan sedang IsTerminating=true dan tidak aman
    // menjalankan apa pun selain menulis satu baris log. API
    // UnhandledExceptionEventArgs sendiri memang tidak punya cara
    // membatalkan terminasi, jadi tidak ada jalur "coba lanjutkan" di sini.
    private void App_AppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception galat)
        {
            PencatatGalat.Catat(galat, sumber: $"AppDomain.UnhandledException (IsTerminating={e.IsTerminating})", level: "FATAL");
        }
    }

    // docs/INVESTIGASI-CRASH-2026-09-21.md §4.3: exception dari Task yang
    // tidak pernah di-await (mis. kalau suatu saat async void lain lupa
    // try/catch sendiri seperti MulaiPenjadwal/JalankanBackupPembukaanAplikasi
    // di atas). SetObserved() HANYA mencegah exception yang SUDAH tercatat
    // di baris sebelumnya ikut mematikan proses lewat finalizer thread —
    // BUKAN perbaikan akar masalah, dan bukan alasan berhenti mencari kenapa
    // Task itu sampai tidak pernah diperiksa.
    private void App_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        PencatatGalat.Catat(e.Exception, sumber: "TaskScheduler.UnobservedTaskException", level: "FATAL");
        e.SetObserved();
    }

    // PRD §16.7 & RENCANA-TUGAS.md Tugas 3: pembukaan hari berjalan "saat
    // aplikasi dibuka". Wizard instalasi pertama (Tugas 13) selalu membuat
    // tahun ajaran aktif sebelum titik ini tercapai, tapi pengaman ini
    // dipertahankan untuk kasus tak terduga (mis. tahun ajaran dinonaktifkan
    // manual di database).
    private void JalankanPembukaanHariBilaSudahDikonfigurasi()
    {
        if (new RepositoriTahunAjaran().CariIdAktif(_connection!) is null)
        {
            return;
        }

        var hariIni = DateOnly.FromDateTime(_penyediaWaktu.Sekarang);
        new PembukaanHari().Jalankan(_connection!, hariIni);
    }

    // PRD §16.1: wizard instalasi pertama. Mengembalikan null kalau wizard
    // ditutup/dibatalkan sebelum selesai — pemanggil (OnStartup) menutup
    // aplikasi pada kasus itu, karena instalasi tanpa sekolah/tahun
    // ajaran/akun tidak bisa dipakai sama sekali.
    private SekolahTersimpan? JalankanWizardInstalasi()
    {
        var viewModel = new WizardInstalasiViewModel(
            _connection!, _penyediaWaktu, new LayananLisensiLokal(), new PenyimpanCloudBackupLokal(LokasiData.FolderBackupCloudStub));
        var window = new WizardInstalasiWindow(viewModel);

        if (window.ShowDialog() != true)
        {
            return null;
        }

        // PRD §18.3 jalur cloud: database yang sedang terbuka (kosong,
        // baru dimigrasi) ditimpa isi backup cloud — koneksi lama harus
        // ditutup dulu (SQLite tidak bisa ditimpa selagi ada koneksi
        // memegangnya), lalu dibuka lagi dan dimigrasi ulang (berjaga-jaga
        // backup itu berasal dari versi aplikasi yang lebih lama).
        if (window.DataDipulihkanDariCloud is { } dataRestore)
        {
            _connection!.Dispose();
            LayananBackup.TerapkanRestore(dataRestore, LokasiData.PathDatabase);
            _connection = SqliteConnectionFactory.Open(LokasiData.PathDatabase);
            new MigrationRunner().Run(_connection);
        }

        return new RepositoriSekolah().CariSatuSatunya(_connection!);
    }

    // PRD §18.1 titik ke-1 ("setiap aplikasi dibuka"). Berjalan di latar
    // belakang (async void — sama seperti detak Penjadwal di
    // MulaiPenjadwal) supaya operator tidak menunggu; galat backup tidak
    // boleh mencegah aplikasi absensi dipakai.
    private async void JalankanBackupPembukaanAplikasi()
    {
        try
        {
            var kunciLisensi = new RepositoriLisensi().BacaStatus(_connection!)?.Kunci;
            await _layananBackup!.JalankanAsync(_connection!, _penyediaWaktu.Sekarang, kunciLisensi);
        }
        catch (Exception galat)
        {
            PencatatGalat.Catat(galat, sumber: "JalankanBackupPembukaanAplikasi", level: "ERROR");
        }
    }

    // PRD §18.4: pengaman restore sudah dijalankan PengaturanViewModel
    // (cadangan kondisi saat ini + jejak audit) sebelum event ini terpicu.
    // Sisanya di sini: tutup koneksi lama supaya berkasnya bisa ditimpa,
    // lalu tutup aplikasi — layar dan ViewModel yang sedang berjalan
    // memegang data dari database LAMA, jadi melanjutkan tanpa membuka
    // ulang aplikasi berisiko menampilkan/menimpa data yang salah.
    private void SelesaikanRestoreDanTutup(byte[] data)
    {
        _connection?.Dispose();
        LayananBackup.TerapkanRestore(data, LokasiData.PathDatabase);

        MessageBox.Show(
            "Restore selesai. Buka kembali aplikasi untuk melanjutkan.",
            "Restore", MessageBoxButton.OK, MessageBoxImage.Information);

        Shutdown();
    }
}

// UAT Bug #11 (2026-09-23): jembatan PresentationTraceSources.DataBindingSource
// -> PencatatGalat (lihat App.OnStartup). WPF menulis kegagalan binding lewat
// TraceListener, bukan exception — tanpa listener ini, kegagalan itu hilang
// begitu saja di build Release (tidak ada Output Window seperti di Visual
// Studio). Pesan trace SUDAH berupa teks siap pakai dari WPF sendiri (path
// properti, nama elemen, dsb.) — dibungkus Exception generik supaya bisa
// lewat PencatatGalat.Catat yang hanya menerima Exception; namespace System
// (bukan Absensi.Domain/Data) jadi TIDAK kena sensor pesan §4.7.2.
internal sealed class PencatatGalatTraceListener : System.Diagnostics.TraceListener
{
    public override void Write(string? message)
    {
    }

    public override void WriteLine(string? message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return;
        }

        Absensi.Data.Diagnostik.PencatatGalat.Catat(new Exception(message), sumber: "WPF.BindingError", level: "ERROR");
    }
}

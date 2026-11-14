using System.Buffers.Binary;
using Absensi.Data;
using Absensi.Data.Akun;
using Absensi.Data.Migrations;
using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;
using Microsoft.JSInterop;

namespace Absensi.Web.Layanan;

// Pemegang basis data dan sesi login web uji. Berkas SQLite hidup di memori
// browser (/tmp) dan disalin ke IndexedDB setelah perubahan supaya data
// bertahan saat halaman dimuat ulang.
public sealed partial class StatusAplikasi
{
    private const string BerkasDb = "/tmp/absensi-web.db";
    private readonly SemaphoreSlim _storage = new(1);
    private readonly Dictionary<string, byte[]> _pendingPhotos = new(StringComparer.Ordinal);
    private uint _storageVersion;

    private readonly IJSRuntime _js;
    private readonly LayananAkun _layananAkun = new();
    private readonly CurrentDayGuard _currentDay = new();

    public StatusAplikasi(IJSRuntime js, JamBrowser jam, ModeAplikasi mode)
    {
        _js = js;
        Jam = jam;
        Mode = mode;
    }

    public ModeAplikasi Mode { get; }

    public JamBrowser Jam { get; }

    public SqliteConnection? Koneksi { get; private set; }

    public bool Siap => Koneksi is not null;

    public AkunTersimpan? Akun { get; private set; }

    public string NamaSekolah { get; private set; } = "";

    public string TahunAjaran { get; private set; } = "";

    public int? TahunAjaranId { get; private set; }

    public string? PesanGalat { get; private set; }

    public string CatatanPenyimpanan { get; private set; } = "";

    public bool StorageFailed { get; private set; }

    public bool StorageConflict { get; private set; }

    // "granted", "denied" or "unsupported"; null until the browser has answered.
    public string? StoragePersistence { get; private set; }

    private Task? _persistenceRequest;

    public event Action? StorageConflictDetected;

    public async Task MulaiAsync()
    {
        try
        {
            var snapshot = await _js.InvokeAsync<byte[]>("absensiStorage.loadDatabase");
            _storageVersion = BinaryPrimitives.ReadUInt32LittleEndian(snapshot);
            var wizardPending = snapshot[4] == 1;
            if (snapshot.Length > 5)
            {
                File.WriteAllBytes(BerkasDb, snapshot.AsSpan(5));
                CatatanPenyimpanan = "Data dipulihkan dari penyimpanan browser.";
            }
            else
            {
                CatatanPenyimpanan = Mode.IsReal ? "Database baru; selesaikan wizard instalasi." : "Data contoh baru dibuat.";
            }

            Buka();
            if (new RepositoriSekolah().CariSatuSatunya(Koneksi!) is null)
            {
                // PANDUAN-UI-UX §9 Wizard instalasi: bila pengguna memilih
                // mulai dari wizard, data contoh tidak dibuat.
                // Mode nyata tidak pernah membuat data contoh.
                if (wizardPending || Mode.IsReal)
                {
                    NeedsInstallation = true;
                    NamaSekolah = "";
                    return;
                }

                DataContoh.Isi(Koneksi!, Jam.Sekarang);
                await SimpanAsync();
            }

            NeedsInstallation = false;
            MuatIdentitas();
            await LoadPhotosAsync();
            StartPersistenceRequest();
        }
        catch (Exception ex)
        {
            PesanGalat = $"Gagal menyiapkan data: {ex.GetType().Name}: {ex.Message}";
        }
    }

    public AkunTersimpan? Masuk(string username, string sandi)
    {
        Akun = _layananAkun.Autentikasi(Koneksi!, username.Trim(), sandi);
        return Akun;
    }

    public void Keluar() => Akun = null;

    public Task SimpanAsync() => SaveSnapshotAsync();

    private async Task SaveSnapshotAsync(string? photoName = null, byte[]? photo = null)
    {
        await _storage.WaitAsync();
        try
        {
            if (StorageConflict) return;
            if (photoName is not null && photo is not null) _pendingPhotos[photoName] = photo;
            using (var command = Koneksi!.CreateCommand())
            {
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                command.ExecuteNonQuery();
            }

            if (!await _js.InvokeAsync<bool>("absensiStorage.saveDatabase", _storageVersion, File.ReadAllBytes(BerkasDb), _pendingPhotos))
            {
                MarkStorageConflict();
                return;
            }

            _storageVersion++;
            _pendingPhotos.Clear();
            if (StorageFailed) CatatanPenyimpanan = "Perubahan berhasil disimpan ulang ke penyimpanan browser.";
            StorageFailed = false;
        }
        catch (Exception)
        {
            StorageFailed = true;
            CatatanPenyimpanan = "Perubahan ada pada sesi ini, tetapi belum tersimpan ke browser. Jangan tutup halaman; coba Simpan ulang di Pengaturan.";
        }
        finally
        {
            _storage.Release();
        }
    }

    private void MarkStorageConflict()
    {
        StorageFailed = StorageConflict = true;
        CatatanPenyimpanan = "Data sudah berubah di tab lain. Perubahan dari tab ini tidak disimpan. Muat ulang halaman sebelum melanjutkan.";
        StorageConflictDetected?.Invoke();
    }

    // Not awaited: some browsers (Firefox) ask the user first, and the answer
    // must not hold back the login screen. Pengaturan shows the result once known.
    private void StartPersistenceRequest()
    {
        if (StoragePersistence == "granted" || _persistenceRequest is { IsCompleted: false }) return;
        _persistenceRequest = RequestPersistenceAsync();
    }

    private async Task RequestPersistenceAsync()
    {
        try { StoragePersistence = await _js.InvokeAsync<string>("absensiStorage.requestPersistence"); }
        catch (Exception ex) when (ex is JSException or TaskCanceledException or InvalidOperationException)
        {
            StoragePersistence = "unsupported";
        }
    }

    public async Task OpenCurrentDayAsync()
    {
        _currentDay.Open(Koneksi!, DateOnly.FromDateTime(Jam.Sekarang));
        await SimpanAsync();
    }

    // Called before every tap and by the gate clock: opens the new day when the
    // application stayed open past midnight (PRD §16.7). Returns true when a day
    // was opened, so screens can reload their attendance lists.
    public async Task<bool> EnsureCurrentDayAsync()
    {
        if (Koneksi is null || NeedsInstallation || StorageConflict) return false;
        if (!_currentDay.EnsureOpened(Koneksi, DateOnly.FromDateTime(Jam.Sekarang))) return false;
        await SimpanAsync();
        return true;
    }

    public async Task RefreshAttendanceDayAsync()
    {
        var years = new RepositoriTahunAjaran();
        TahunAjaranId = years.CariIdAktif(Koneksi!);
        TahunAjaran = years.CariNamaAktif(Koneksi!) ?? "";
        await OpenCurrentDayAsync();
    }

    // True sampai wizard instalasi web diselesaikan (database masih kosong).
    public bool NeedsInstallation { get; private set; }

    public async Task CompleteInstallationAsync()
    {
        MuatIdentitas();
        await SimpanAsync();
        StartPersistenceRequest();
        NeedsInstallation = StorageFailed;
        if (!StorageFailed) CatatanPenyimpanan = "Instalasi selesai; data tersimpan di browser ini.";
    }

    public async Task<bool> ResetAsync(bool sampleData = true)
    {
        await _storage.WaitAsync();
        try
        {
            if (StorageConflict) return false;
            if (!await _js.InvokeAsync<bool>("absensiStorage.resetDatabase", _storageVersion, !sampleData))
            {
                MarkStorageConflict();
                return false;
            }

            Akun = null;
            _currentDay.Forget();
            Koneksi?.Dispose();
            Koneksi = null;
            SqliteConnection.ClearAllPools();
            DeleteDatabaseFiles(BerkasDb);

            _pendingPhotos.Clear();
            ClearLocalPhotos();
            StorageFailed = false;
        }
        finally
        {
            _storage.Release();
        }

        await MulaiAsync();
        return true;
    }

    private void Buka()
    {
        Koneksi = SqliteConnectionFactory.Open(BerkasDb);
        new MigrationRunner().Run(Koneksi);
    }

    // Dipanggil setelah identitas sekolah diubah di Pengaturan supaya judul
    // aplikasi dan kepala laporan memakai nama yang tersimpan.
    public void RefreshSchoolName()
    {
        var school = new RepositoriSekolah().CariSatuSatunya(Koneksi!);
        NamaSekolah = school?.Nama ?? "";
        LogoName = school?.LogoPath;
    }

    // Nama berkas logo (disimpan bersama foto di IndexedDB); null bila belum ada.
    public string? LogoName { get; private set; }

    private void MuatIdentitas()
    {
        RefreshSchoolName();
        var repoTahun = new RepositoriTahunAjaran();
        TahunAjaranId = repoTahun.CariIdAktif(Koneksi!);
        TahunAjaran = repoTahun.CariNamaAktif(Koneksi!) ?? "";

        // PRD §16.7: hari dibuka saat aplikasi dibuka (idempoten).
        _currentDay.Open(Koneksi!, DateOnly.FromDateTime(Jam.Sekarang));
    }
}

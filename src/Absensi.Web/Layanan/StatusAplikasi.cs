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

    public StatusAplikasi(IJSRuntime js, JamBrowser jam)
    {
        _js = js;
        Jam = jam;
    }

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
                CatatanPenyimpanan = "Data contoh baru dibuat.";
            }

            Buka();
            if (new RepositoriSekolah().CariSatuSatunya(Koneksi!) is null)
            {
                // PANDUAN-UI-UX §9 Wizard instalasi: bila pengguna memilih
                // mulai dari wizard, data contoh tidak dibuat.
                if (wizardPending)
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
    }

    public async Task OpenCurrentDayAsync()
    {
        new PembukaanHari().Jalankan(Koneksi!, DateOnly.FromDateTime(Jam.Sekarang));
        await SimpanAsync();
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
        NeedsInstallation = StorageFailed;
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
            Koneksi?.Dispose();
            Koneksi = null;
            SqliteConnection.ClearAllPools();
            foreach (var path in new[] { BerkasDb, BerkasDb + "-wal", BerkasDb + "-shm" })
                File.Delete(path);

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
        new PembukaanHari().Jalankan(Koneksi!, DateOnly.FromDateTime(Jam.Sekarang));
    }
}

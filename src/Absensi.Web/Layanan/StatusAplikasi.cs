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
public sealed class StatusAplikasi
{
    private const string BerkasDb = "/tmp/absensi-web.db";
    private const string KunciPenyimpanan = "absensi.db";

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

    public async Task MulaiAsync()
    {
        try
        {
            var tersimpan = await _js.InvokeAsync<byte[]?>("absensiStorage.load", KunciPenyimpanan);
            if (tersimpan is { Length: > 0 })
            {
                File.WriteAllBytes(BerkasDb, tersimpan);
                CatatanPenyimpanan = "Data dipulihkan dari penyimpanan browser.";
            }
            else
            {
                CatatanPenyimpanan = "Data contoh baru dibuat.";
            }

            Buka();
            if (new RepositoriSekolah().CariSatuSatunya(Koneksi!) is null)
            {
                DataContoh.Isi(Koneksi!, Jam.Sekarang);
                await SimpanAsync();
            }

            MuatIdentitas();
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

    public async Task SimpanAsync()
    {
        try
        {
            using (var perintah = Koneksi!.CreateCommand())
            {
                perintah.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                perintah.ExecuteNonQuery();
            }

            await _js.InvokeVoidAsync("absensiStorage.save", KunciPenyimpanan, File.ReadAllBytes(BerkasDb));
        }
        catch (Exception ex)
        {
            CatatanPenyimpanan = $"Penyimpanan ke browser gagal: {ex.Message}";
        }
    }

    public async Task ResetAsync()
    {
        Akun = null;
        Koneksi?.Dispose();
        Koneksi = null;
        SqliteConnection.ClearAllPools();
        foreach (var berkas in new[] { BerkasDb, BerkasDb + "-wal", BerkasDb + "-shm" })
        {
            if (File.Exists(berkas))
            {
                File.Delete(berkas);
            }
        }

        await _js.InvokeVoidAsync("absensiStorage.remove", KunciPenyimpanan);
        await MulaiAsync();
    }

    private void Buka()
    {
        Koneksi = SqliteConnectionFactory.Open(BerkasDb);
        new MigrationRunner().Run(Koneksi);
    }

    private void MuatIdentitas()
    {
        NamaSekolah = new RepositoriSekolah().CariSatuSatunya(Koneksi!)?.Nama ?? "";
        var repoTahun = new RepositoriTahunAjaran();
        TahunAjaranId = repoTahun.CariIdAktif(Koneksi!);
        TahunAjaran = repoTahun.CariNamaAktif(Koneksi!) ?? "";

        // PRD §16.7: hari dibuka saat aplikasi dibuka (idempoten).
        new PembukaanHari().Jalankan(Koneksi!, DateOnly.FromDateTime(Jam.Sekarang));
    }
}

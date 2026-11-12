using Absensi.Data.MasterData;
using Microsoft.JSInterop;

namespace Absensi.Web.Layanan;

// Student/teacher photos for the browser test (PRD §5.5, §16.4 langkah 6).
// The database keeps only the file name (siswa/guru.foto_path, as on desktop:
// "<NISN>.png" / "<NIP>.png"); the 200×200 PNG bytes live in IndexedDB under
// "foto/<name>", are loaded into memory once when the app opens, and are also
// written to /tmp/foto so file-based report code can find them.
public sealed partial class StatusAplikasi
{
    public const string PhotoFolder = "/tmp/foto";
    private const string PhotoKeyPrefix = "foto/";
    private const long MaxPhotoUploadBytes = 8 * 1024 * 1024;
    private readonly Dictionary<string, string> _photoUrls = new(StringComparer.Ordinal);

    public long MaxPhotoBytes => MaxPhotoUploadBytes;

    public string? PhotoUrl(string? photoName) =>
        photoName is not null && _photoUrls.TryGetValue(photoName, out var url) ? url : null;

    public string? PhotoFile(string? photoName) =>
        photoName is not null && File.Exists(Path.Combine(PhotoFolder, photoName)) ? Path.Combine(PhotoFolder, photoName) : null;

    // Returns null on success, otherwise a message for the operator. Success is
    // reported only after the photo is stored AND the database change is saved.
    public async Task<string?> UploadPhotoAsync(PersonKind kind, int id, string identity, byte[] original)
    {
        // T6: check the role before anything is stored (PRD §3: master data is full-authority only).
        if (!WorkspaceView.CanManage(this)) return "Akun ini tidak berwenang mengubah foto.";
        if (original.Length == 0) return "Berkas foto kosong.";

        byte[]? resized;
        try { resized = await _js.InvokeAsync<byte[]?>("absensiPhoto.resize", original); }
        catch (JSException) { resized = null; }
        if (resized is not { Length: > 0 }) return "Berkas tidak dikenali sebagai gambar. Gunakan JPG atau PNG.";

        var name = $"{identity}.png";
        try { await _js.InvokeVoidAsync("absensiStorage.save", PhotoKeyPrefix + name, resized); }
        catch (JSException) { return "Foto belum dapat disimpan ke penyimpanan browser. Coba kembali."; }

        try
        {
            if (kind == PersonKind.Student) new LayananSiswa().SimpanFoto(Koneksi!, Akun!, Jam.Sekarang, id, name);
            else new LayananGuru().SimpanFoto(Koneksi!, Akun!, Jam.Sekarang, id, name);
        }
        catch (InvalidOperationException ex) { return ex.Message; }
        catch (Microsoft.Data.Sqlite.SqliteException) { return "Data foto belum dapat dicatat. Coba kembali."; }

        CachePhoto(name, resized);
        await SimpanAsync();
        return StorageFailed ? CatatanPenyimpanan : null;
    }

    public const string LogoFileName = "logo-sekolah.png";

    // Logo sekolah untuk judul aplikasi dan kepala laporan (PANDUAN-UI-UX §11).
    // Same storage path as photos; success only after the database change is saved.
    public async Task<string?> UploadLogoAsync(byte[] original)
    {
        if (!WorkspaceView.CanManage(this)) return "Akun ini tidak berwenang mengubah logo.";
        if (original.Length == 0) return "Berkas logo kosong.";

        byte[]? resized;
        try { resized = await _js.InvokeAsync<byte[]?>("absensiPhoto.resize", original); }
        catch (JSException) { resized = null; }
        if (resized is not { Length: > 0 }) return "Berkas tidak dikenali sebagai gambar. Gunakan JPG atau PNG.";

        try { await _js.InvokeVoidAsync("absensiStorage.save", PhotoKeyPrefix + LogoFileName, resized); }
        catch (JSException) { return "Logo belum dapat disimpan ke penyimpanan browser. Coba kembali."; }

        try { new Absensi.Data.Pengaturan.SchoolIdentityService().SetLogo(Koneksi!, Akun!, Jam.Sekarang, LogoFileName); }
        catch (InvalidOperationException ex) { return ex.Message; }
        catch (Microsoft.Data.Sqlite.SqliteException) { return "Data logo belum dapat dicatat. Coba kembali."; }

        CachePhoto(LogoFileName, resized);
        RefreshSchoolName();
        await SimpanAsync();
        return StorageFailed ? CatatanPenyimpanan : null;
    }

    private async Task LoadPhotosAsync()
    {
        _photoUrls.Clear();
        string[] keys;
        try { keys = await _js.InvokeAsync<string[]>("absensiStorage.keys", PhotoKeyPrefix); }
        catch (JSException) { return; }

        foreach (var key in keys)
        {
            var bytes = await _js.InvokeAsync<byte[]?>("absensiStorage.load", key);
            if (bytes is { Length: > 0 }) CachePhoto(key[PhotoKeyPrefix.Length..], bytes);
        }
    }

    private async Task RemovePhotosAsync()
    {
        try
        {
            foreach (var key in await _js.InvokeAsync<string[]>("absensiStorage.keys", PhotoKeyPrefix))
            {
                await _js.InvokeVoidAsync("absensiStorage.remove", key);
            }
        }
        catch (JSException) { }

        _photoUrls.Clear();
        if (Directory.Exists(PhotoFolder)) Directory.Delete(PhotoFolder, recursive: true);
    }

    private void CachePhoto(string name, byte[] bytes)
    {
        _photoUrls[name] = "data:image/png;base64," + Convert.ToBase64String(bytes);
        Directory.CreateDirectory(PhotoFolder);
        File.WriteAllBytes(Path.Combine(PhotoFolder, name), bytes);
    }
}

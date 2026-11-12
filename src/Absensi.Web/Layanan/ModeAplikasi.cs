namespace Absensi.Web.Layanan;

// Mode web dibaca dari wwwroot/appsettings.json ("Mode": "uji" atau "nyata").
// Uji: data contoh, akun contoh, jam skenario (situs absensi-uji).
// Nyata: uji coba dengan data sekolah sungguhan; mulai dari wizard kosong,
// tanpa data/akun contoh dan tanpa jam skenario (keputusan pemilik
// 12 November 2026, PRD §25.5). Data tetap hanya di browser perangkat itu.
public sealed class ModeAplikasi(bool isReal)
{
    public bool IsReal { get; } = isReal;

    public string EnvironmentLabel => IsReal ? "Uji coba data asli" : "Lingkungan uji";

    public static ModeAplikasi FromSetting(string? value) =>
        new(string.Equals(value?.Trim(), "nyata", StringComparison.OrdinalIgnoreCase));
}

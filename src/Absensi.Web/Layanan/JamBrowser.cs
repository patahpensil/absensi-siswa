using Absensi.Domain;

namespace Absensi.Web.Layanan;

// Jam untuk web uji: WITA (UTC+8, zona SMP Negeri 1 Pancarijang) ditambah
// selisih "jam simulasi" supaya jadwal pagi bisa diuji kapan saja.
public sealed class JamBrowser : IPenyediaWaktu
{
    private const int SelisihWitaJam = 8;

    public TimeSpan SelisihSimulasi { get; set; } = TimeSpan.Zero;

    public DateTime Sekarang => DateTime.UtcNow.AddHours(SelisihWitaJam) + SelisihSimulasi;
}

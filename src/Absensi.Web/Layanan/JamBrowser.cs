using Absensi.Domain;

namespace Absensi.Web.Layanan;

// Jam untuk web uji: WITA (UTC+8, zona SMP Negeri 1 Pancarijang) ditambah
// selisih "jam simulasi" supaya jadwal pagi bisa diuji kapan saja.
public sealed class JamBrowser : IPenyediaWaktu
{
    private const int SelisihWitaJam = 8;

    public TimeSpan SelisihSimulasi { get; set; } = TimeSpan.Zero;

    private DateTime? scenarioTime;
    public bool IsScenario => scenarioTime is not null || SelisihSimulasi != TimeSpan.Zero;
    public DateTime Sekarang => scenarioTime ?? DateTime.UtcNow.AddHours(SelisihWitaJam) + SelisihSimulasi;

    // A frozen test clock makes second-boundary scenarios reproducible in the browser.
    public void SetScenario(DateTime value) { scenarioTime = DateTime.SpecifyKind(value, DateTimeKind.Unspecified); SelisihSimulasi = TimeSpan.Zero; }
    public void ResetScenario() { scenarioTime = null; SelisihSimulasi = TimeSpan.Zero; }
    public void Advance(TimeSpan amount) { if (scenarioTime is { } time) scenarioTime = time + amount; else SelisihSimulasi += amount; }
}

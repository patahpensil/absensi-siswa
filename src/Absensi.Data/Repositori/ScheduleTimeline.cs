namespace Absensi.Data.Repositori;

// One weekday row of a schedule version and the date it applies from (PRD §7 P-15).
public sealed record ScheduleChange(DateOnly EffectiveFrom, JadwalHariBaris Row);

// Resolves the schedule that applied on a given date: the latest change
// for that weekday effective on or before the date, otherwise the period's
// base schedule (jadwal_hari).
public sealed class ScheduleTimeline
{
    private readonly IReadOnlyDictionary<int, JadwalHariBaris> _base;
    private readonly IReadOnlyList<ScheduleChange> _changes;

    public ScheduleTimeline(IEnumerable<JadwalHariBaris> baseRows, IReadOnlyList<ScheduleChange> changes)
    {
        _base = baseRows.ToDictionary(row => row.Hari);
        _changes = changes.OrderBy(change => change.EffectiveFrom).ToList();
    }

    // Null when neither a change nor a stored base row exists for the day;
    // callers keep their existing meaning for "no schedule" (not a school day).
    public JadwalHariBaris? For(DateOnly date, int day) =>
        _changes.LastOrDefault(change => change.Row.Hari == day && change.EffectiveFrom <= date)?.Row
        ?? (_base.TryGetValue(day, out var row) ? row : null);
}

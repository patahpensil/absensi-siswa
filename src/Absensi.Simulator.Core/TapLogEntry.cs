using Absensi.Domain.Kehadiran;

namespace Absensi.Simulator.Core;

public sealed record TapLogEntry(DateTime Time, string CardUid, string? PersonName, KeputusanTap Decision)
{
    public string Description => StatusLabels.Decision(Decision);
}

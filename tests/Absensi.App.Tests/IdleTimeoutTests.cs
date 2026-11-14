using Absensi.App.Shell;
using Absensi.Domain;

namespace Absensi.App.Tests;

// PRD §20 P-24 point 8: the working window signs out after 10 minutes idle.
public sealed class IdleTimeoutTests
{
    private sealed class ManualClock(DateTime start) : IPenyediaWaktu
    {
        public DateTime Sekarang { get; set; } = start;
    }

    private static readonly DateTime Start = new(2026, 11, 16, 8, 0, 0);

    [Fact]
    public void Expires_AfterTenMinutesWithoutActivity()
    {
        var clock = new ManualClock(Start);
        var idle = new IdleTimeout(clock);

        clock.Sekarang = Start.AddMinutes(10).AddSeconds(-1);
        Assert.False(idle.IsExpired);
        clock.Sekarang = Start.AddMinutes(10);
        Assert.True(idle.IsExpired);
    }

    [Fact]
    public void Activity_RestartsTheTenMinutes()
    {
        var clock = new ManualClock(Start);
        var idle = new IdleTimeout(clock);

        clock.Sekarang = Start.AddMinutes(9);
        idle.RecordActivity();
        clock.Sekarang = Start.AddMinutes(18);

        Assert.False(idle.IsExpired);
        clock.Sekarang = Start.AddMinutes(19);
        Assert.True(idle.IsExpired);
    }

    [Fact]
    public void ClockMovedBackwards_RestartsInsteadOfStayingOpenForever()
    {
        var clock = new ManualClock(Start);
        var idle = new IdleTimeout(clock);

        clock.Sekarang = Start.AddHours(-2);
        Assert.False(idle.IsExpired);
        clock.Sekarang = Start.AddHours(-2).AddMinutes(10);
        Assert.True(idle.IsExpired);
    }
}

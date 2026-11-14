using Absensi.Domain;

namespace Absensi.App.Shell;

// PRD §20 P-24 point 8: the working window signs out after 10 minutes without
// input. The gate window keeps running (WindowActivity ignores its input).
public sealed class IdleTimeout
{
    public static readonly TimeSpan Limit = TimeSpan.FromMinutes(10);

    private readonly IPenyediaWaktu _clock;
    private DateTime _lastActivity;

    public IdleTimeout(IPenyediaWaktu clock)
    {
        _clock = clock;
        _lastActivity = clock.Sekarang;
    }

    public void RecordActivity() => _lastActivity = _clock.Sekarang;

    // A clock moved backwards (manual correction) restarts the count instead of
    // keeping the session open forever.
    public bool IsExpired
    {
        get
        {
            var now = _clock.Sekarang;
            if (now < _lastActivity)
            {
                _lastActivity = now;
                return false;
            }

            return now - _lastActivity >= Limit;
        }
    }
}

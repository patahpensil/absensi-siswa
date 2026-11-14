using Absensi.Data.Repositori;
using Microsoft.Data.Sqlite;

namespace Absensi.Data;

// PRD §16.7: the attendance day that matches the clock must be opened before a
// tap is processed. A tap only updates an existing attendance row
// (RepositoriAbsensi.SimpanKeputusan*), so on an application left running past
// midnight the first taps of the new day would otherwise change nothing while
// the screen still reports success.
//
// One instance per open connection. Not thread-safe: callers run on a single
// UI thread (Blazor WebAssembly, WPF dispatcher).
public sealed class CurrentDayGuard
{
    private readonly PembukaanHari _dayOpening = new();
    private readonly RepositoriTahunAjaran _academicPeriods = new();

    public DateOnly? OpenedDay { get; private set; }

    // Always runs the day opening, for callers that changed data the opening
    // depends on (calendar, active period, newly added students).
    public void Open(SqliteConnection connection, DateOnly day)
    {
        _dayOpening.Jalankan(connection, day);
        OpenedDay = day;
    }

    // Opens the day only when it differs from the last opened day. Returns
    // true when the day was opened by this call. Without an active academic
    // period there is nothing to open; the tap itself reports that state.
    public bool EnsureOpened(SqliteConnection connection, DateOnly day)
    {
        if (OpenedDay == day || _academicPeriods.CariIdAktif(connection) is null)
        {
            return false;
        }

        Open(connection, day);
        return true;
    }

    // Called when the connection is replaced (reset, new database).
    public void Forget() => OpenedDay = null;
}

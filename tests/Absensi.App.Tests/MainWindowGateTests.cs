using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Absensi.App.Shell;
using Absensi.Data.Backup;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// P-24: the gate window outlives the working window that opened it. A signed-out
// working window must not stay reachable from the running gate window.
public sealed class MainWindowGateTests
{
    private static readonly DateTime Time = new(2026, 9, 25, 8, 0, 0);

    [Fact]
    public void ClosedWorkingWindow_IsNotKeptAliveByRunningGate() => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        database.SeedKelasUntukRegistrasiKartu();
        var gate = OffScreen(new Window());
        var opened = 0;
        Window OpenGate()
        {
            opened++;
            return gate;
        }

        try
        {
            var first = OpenAndCloseSession(database, OpenGate);
            var second = OpenAndCloseSession(database, OpenGate);
            Pump(4);
            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Pump(1);
            }

            Assert.Equal(2, opened);
            Assert.True(gate.IsVisible);
            Assert.False(first.IsAlive, "Jendela kerja sesi pertama masih tertahan oleh jendela gerbang.");
            Assert.False(second.IsAlive, "Jendela kerja sesi kedua masih tertahan oleh jendela gerbang.");
        }
        finally
        {
            gate.Close();
        }
    });

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference OpenAndCloseSession(MigratedDatabase database, Func<Window> openGate)
    {
        var account = new AkunTersimpan(1, "admin1", "x", PeranAkun.Admin, GuruId: null, true);
        var viewModel = new MainWindowViewModel(
            database.Connection,
            account,
            new PenyediaWaktuUji(Time),
            new StatusModeRegistrasi(),
            "SMP Contoh",
            logoPath: null,
            tahunAjaranAktif: "2026/2027",
            tahunAjaranId: 1,
            new LayananBackup(
                Path.Combine(Path.GetTempPath(), $"absensi-gate-backup-{Guid.NewGuid():N}"),
                new PenyimpanCloudBackupLokal(Path.Combine(Path.GetTempPath(), $"absensi-gate-cloud-{Guid.NewGuid():N}"))));
        var window = OffScreen(new MainWindow(viewModel, openGate, () => null, _ => { }, new PenyediaWaktuUji(Time)));
        window.WindowState = WindowState.Normal;
        window.Show();
        viewModel.BukaGerbangCommand.Execute(null);
        window.Close();
        return new WeakReference(window);
    }

    private static T OffScreen<T>(T window) where T : Window
    {
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -6000;
        window.Top = 0;
        window.Width = 1280;
        window.Height = 720;
        return window;
    }

    private static void Pump(int count)
    {
        for (var i = 0; i < count; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }
}

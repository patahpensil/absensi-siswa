using System.Windows;
using System.Windows.Threading;
using Absensi.App.Gerbang;

namespace Absensi.App.Tests;

// PRD §20 P-24 point 3: a dialog of the working window must not freeze the
// gate window. Real WPF windows on the shared STA thread (StaHost).
public sealed class GateStaysEnabledTests
{
    [Fact]
    public void ModalDialog_AttachedWindowStaysEnabled_OtherWindowsAreDisabled() => StaHost.Jalankan(() =>
    {
        var gate = OffScreen(new Window());
        gate.SourceInitialized += (_, _) => GateStaysEnabled.Attach(gate);
        var other = OffScreen(new Window());
        gate.Show();
        other.Show();
        bool? gateEnabled = null, otherEnabled = null;
        var dialog = OffScreen(new Window());
        dialog.Loaded += (_, _) => dialog.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
        {
            gateEnabled = GateStaysEnabled.IsEnabled(gate);
            otherEnabled = GateStaysEnabled.IsEnabled(other);
            dialog.Close();
        });

        try
        {
            dialog.ShowDialog();

            Assert.True(gateEnabled);
            Assert.False(otherEnabled);
            Assert.True(GateStaysEnabled.IsEnabled(other));
            Assert.True(GateStaysEnabled.IsEnabled(gate));
        }
        finally
        {
            gate.Close();
            other.Close();
        }
    });

    private static Window OffScreen(Window window)
    {
        window.ShowInTaskbar = false;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = -6000;
        window.Top = 0;
        window.Width = 200;
        window.Height = 120;
        return window;
    }
}

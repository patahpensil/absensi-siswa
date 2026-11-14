using System.Windows;
using Absensi.App.Gerbang;

namespace Absensi.App.Tests;

// PRD §20 P-24 point 3: a dialog of the working window must not freeze the
// gate window. Real WPF windows on the shared STA thread (StaHost).
public sealed class GateStaysEnabledTests
{
    private sealed class FakeGate : Window, IKeepsRunningDuringDialogs;

    [Fact]
    public void ModalDialog_GateWindowStaysEnabled_OtherWindowsAreDisabled() => StaHost.Jalankan(() =>
    {
        GateStaysEnabled.Install();
        var gate = OffScreen(new FakeGate());
        var other = OffScreen(new Window());
        gate.Show();
        other.Show();
        bool? gateEnabled = null, otherEnabled = null;
        var dialog = OffScreen(new Window());
        dialog.Loaded += (_, _) =>
        {
            gateEnabled = GateStaysEnabled.IsEnabled(gate);
            otherEnabled = GateStaysEnabled.IsEnabled(other);
            dialog.Close();
        };

        try
        {
            dialog.ShowDialog();

            Assert.True(gateEnabled);
            Assert.False(otherEnabled);
            Assert.True(GateStaysEnabled.IsEnabled(other));
        }
        finally
        {
            gate.Close();
            other.Close();
        }
    });

    private static T OffScreen<T>(T window) where T : Window
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

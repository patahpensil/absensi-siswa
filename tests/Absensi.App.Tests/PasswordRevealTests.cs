using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using Absensi.App.Login;
using Absensi.App.Perilaku;

namespace Absensi.App.Tests;

// Eye toggle on password fields (KolomSandiStyle + PasswordReveal), checked on
// the real LoginWindow. Shown off-screen and hidden afterwards, not closed
// (see StaHost).
public sealed class PasswordRevealTests
{
    [Fact]
    public void Toggle_ShowsAndHidesPassword_AndVisibleTypingUpdatesPasswordBox() => StaHost.Jalankan(() =>
    {
        using var database = new MigratedDatabase();
        var window = new LoginWindow(new LoginViewModel(database.Connection, "1.0.0", "SMP Contoh", null, statusLisensi: null))
        {
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -6000,
            Top = 0,
        };
        window.Show();
        try
        {
            Pump();
            var box = (PasswordBox)window.FindName("KolomSandi");
            var text = (TextBox)box.Template.FindName(PasswordReveal.TextPartName, box);
            var toggle = (ToggleButton)box.Template.FindName(PasswordReveal.TogglePartName, box);
            var changes = 0;
            box.PasswordChanged += (_, _) => changes++;

            box.Password = "Rahasia123";
            Assert.Equal(Visibility.Collapsed, text.Visibility);

            toggle.IsChecked = true;
            Pump();
            Assert.Equal(Visibility.Visible, text.Visibility);
            Assert.Equal("Rahasia123", text.Text);

            text.Text = "Rahasia1234";
            Assert.Equal("Rahasia1234", box.Password);

            // A window clearing the field while it is shown keeps both equal.
            box.Clear();
            Assert.Equal("", text.Text);
            box.Password = "Akhir99x";
            Assert.Equal("Akhir99x", text.Text);

            toggle.IsChecked = false;
            Pump();
            Assert.Equal(Visibility.Collapsed, text.Visibility);
            Assert.Equal("", text.Text);
            Assert.Equal("Akhir99x", box.Password);
            Assert.True(changes >= 4);
        }
        finally
        {
            window.Hide();
        }
    });

    private static void Pump()
    {
        for (var i = 0; i < 4; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }
}

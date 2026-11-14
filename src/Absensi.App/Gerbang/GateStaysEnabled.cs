using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Absensi.App.Gerbang;

// Windows that keep receiving input while a dialog of the working window is
// open (PRD §20 P-24 point 3: the gate window runs separately from the login
// session).
internal interface IKeepsRunningDuringDialogs;

// WPF's ShowDialog disables every other top-level window of the thread, which
// would freeze the gate window (and hold back card taps) while, for example,
// the card registration dialog is open. When a modal window loads, the gate
// window is enabled again. WPF re-enables the windows it disabled when the
// dialog closes, so nothing has to be restored. Win32 MessageBox dialogs are
// not covered.
internal static class GateStaysEnabled
{
    private static bool _installed;

    public static void Install()
    {
        if (_installed)
        {
            return;
        }

        _installed = true;
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnWindowLoaded));
    }

    public static bool IsEnabled(Window window) => IsWindowEnabled(new WindowInteropHelper(window).Handle);

    private static void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is IKeepsRunningDuringDialogs || !System.Windows.Interop.ComponentDispatcher.IsThreadModal)
        {
            return;
        }

        if (Application.Current is null)
        {
            return;
        }

        foreach (Window window in Application.Current.Windows)
        {
            if (window is IKeepsRunningDuringDialogs && window.IsVisible)
            {
                EnableWindow(new WindowInteropHelper(window).Handle, true);
            }
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool enable);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr hWnd);
}

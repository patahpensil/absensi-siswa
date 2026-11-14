using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Absensi.App.Gerbang;

// PRD §20 P-24 point 3: the gate window runs separately from the working
// window. WPF's ShowDialog (and Win32 message boxes) disable every other
// top-level window of the thread, which would freeze the gate window and hold
// back card taps while, for example, the card registration dialog is open.
// The gate window therefore enables itself again whenever Windows disables it.
internal static class GateStaysEnabled
{
    private const int WmEnable = 0x000A;

    // Call once the window handle exists (SourceInitialized).
    public static void Attach(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var source = HwndSource.FromHwnd(handle)
            ?? throw new InvalidOperationException("Jendela belum memiliki handle.");
        source.AddHook((IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (message == WmEnable && wParam == IntPtr.Zero)
            {
                window.Dispatcher.BeginInvoke(DispatcherPriority.Send, () =>
                {
                    if (window.IsLoaded)
                    {
                        EnableWindow(hwnd, true);
                    }
                });
            }

            return IntPtr.Zero;
        });
    }

    public static bool IsEnabled(Window window) => IsWindowEnabled(new WindowInteropHelper(window).Handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool enable);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr hWnd);
}

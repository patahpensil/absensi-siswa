using System.Windows;
using System.Windows.Input;
using Absensi.App.Gerbang;

namespace Absensi.App.Shell;

// Reports keyboard, mouse and touch input from any application window except
// the gate window, so dialogs opened from the working window count as use of
// it while card readers typing into the gate window do not keep a session open.
// Class handlers cannot be removed; subscribers must unsubscribe from Input.
public static class WindowActivity
{
    private static bool _installed;

    public static event Action? Input;

    public static void Install()
    {
        if (_installed)
        {
            return;
        }

        _installed = true;
        Register(UIElement.PreviewKeyDownEvent);
        Register(UIElement.PreviewMouseDownEvent);
        Register(UIElement.PreviewMouseMoveEvent);
        Register(UIElement.PreviewMouseWheelEvent);
        Register(UIElement.PreviewTouchDownEvent);
    }

    private static void Register(RoutedEvent routedEvent) =>
        EventManager.RegisterClassHandler(typeof(Window), routedEvent, new RoutedEventHandler(OnInput), handledEventsToo: true);

    private static void OnInput(object sender, RoutedEventArgs e)
    {
        if (sender is Window and not GerbangWindow)
        {
            Input?.Invoke();
        }
    }
}

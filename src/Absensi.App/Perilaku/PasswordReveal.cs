using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Absensi.App.Perilaku;

// Show/hide toggle for PasswordBox (ASVS 5.0 6.2.6 allows temporarily viewing
// the whole masked password). KolomSandiStyle puts a plain TextBox and a
// ToggleButton in the PasswordBox template; this behavior keeps the two in
// step. Typing in the visible TextBox writes PasswordBox.Password, so the
// existing PasswordChanged handlers in each window keep feeding the view
// models unchanged. All handlers are attached to the PasswordBox and its own
// template parts, so they live and die with the control.
public static class PasswordReveal
{
    public const string TextPartName = "PART_RevealText";
    public const string TogglePartName = "PART_RevealToggle";

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(PasswordReveal), new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty IsWiredProperty = DependencyProperty.RegisterAttached(
        "IsWired", typeof(bool), typeof(PasswordReveal), new PropertyMetadata(false));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox box || e.NewValue is not true)
        {
            return;
        }

        // The style sets this before its Template setter is applied, so the
        // template parts are looked up once the control is loaded.
        if (box.IsLoaded)
        {
            Wire(box);
        }
        else
        {
            box.Loaded += OnLoaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var box = (PasswordBox)sender;
        box.Loaded -= OnLoaded;
        Wire(box);
    }

    private static void Wire(PasswordBox box)
    {
        if ((bool)box.GetValue(IsWiredProperty))
        {
            return;
        }

        if (box.Template?.FindName(TextPartName, box) is not TextBox text
            || box.Template.FindName(TogglePartName, box) is not ToggleButton toggle)
        {
            return;
        }

        box.SetValue(IsWiredProperty, true);
        var syncing = false;

        toggle.Checked += (_, _) =>
        {
            syncing = true;
            text.Text = box.Password;
            syncing = false;
            text.Visibility = Visibility.Visible;
            text.Focus();
            text.CaretIndex = text.Text.Length;
        };
        toggle.Unchecked += (_, _) =>
        {
            text.Visibility = Visibility.Collapsed;
            syncing = true;
            text.Text = string.Empty;
            syncing = false;
            box.Focus();
        };
        text.TextChanged += (_, _) =>
        {
            if (syncing)
            {
                return;
            }

            syncing = true;
            box.Password = text.Text;
            syncing = false;
        };
        box.PasswordChanged += (_, _) =>
        {
            // A window may clear the password (e.g. after a failed attempt)
            // while it is shown; keep the visible copy equal.
            if (syncing || toggle.IsChecked != true)
            {
                return;
            }

            syncing = true;
            text.Text = box.Password;
            syncing = false;
        };
    }
}

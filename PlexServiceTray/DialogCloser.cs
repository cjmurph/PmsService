using System.Windows;

namespace PlexServiceTray
{
    /// <summary>
    /// Behaviour for a window to listen for a view models DialogResult changing
    /// </summary>
    public static class DialogCloser
    {
        public static readonly DependencyProperty DialogResultProperty =
            DependencyProperty.RegisterAttached(
                "DialogResult",
                typeof(bool?),
                typeof(DialogCloser),
                new PropertyMetadata(DialogResultChanged));

        private static void DialogResultChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e) {
            if (d is not Window window) {
                return;
            }

            if (System.Windows.Interop.ComponentDispatcher.IsThreadModal)
            {
                window.DialogResult = e.NewValue as bool?;
            }
            else
            {
                window.Close();
            }
        }
        public static void SetDialogResult(Window target, bool? value)
        {
            target.SetValue(DialogResultProperty, value);
        }
    }
}

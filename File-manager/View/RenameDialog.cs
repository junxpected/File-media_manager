using System.Windows;

namespace File_manager.View
{
    public partial class RenameDialog : Window
    {
        public string NewName { get; private set; } = string.Empty;

        public RenameDialog(string currentName)
        {
            Width = 380; Height = 140;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = System.Windows.Media.Brushes.Transparent;
            Title = "Rename";

            var border = new System.Windows.Controls.Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x25, 0x25, 0x26)),
                CornerRadius = new CornerRadius(6)
            };

            var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(16) };

            var label = new System.Windows.Controls.TextBlock
            {
                Text = "New name:",
                Foreground = System.Windows.Media.Brushes.Gray,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 6)
            };

            var input = new System.Windows.Controls.TextBox
            {
                Text = currentName,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xCC)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 13,
                CaretBrush = System.Windows.Media.Brushes.White
            };
            input.SelectAll();
            input.Focus();

            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };

            var btnOk = new System.Windows.Controls.Button
            {
                Content = "Rename",
                Width = 80, Height = 28,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xCC)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(8, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnOk.Click += (_, __) =>
            {
                NewName = input.Text.Trim();
                if (string.IsNullOrEmpty(NewName)) return;
                DialogResult = true;
            };

            var btnCancel = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Width = 70, Height = 28,
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x3E, 0x3E, 0x3E)),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            btnCancel.Click += (_, __) => DialogResult = false;

            input.KeyDown += (_, ke) =>
            {
                if (ke.Key == System.Windows.Input.Key.Enter) btnOk.RaiseEvent(
                    new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
                if (ke.Key == System.Windows.Input.Key.Escape) btnCancel.RaiseEvent(
                    new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            };

            btnPanel.Children.Add(btnCancel);
            btnPanel.Children.Add(btnOk);
            stack.Children.Add(label);
            stack.Children.Add(input);
            stack.Children.Add(btnPanel);
            border.Child = stack;
            Content = border;
        }
    }
}
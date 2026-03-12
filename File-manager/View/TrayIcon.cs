using System.Windows;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using File_manager.Services;

namespace File_manager.View
{
    public class TrayIcon : IDisposable
    {
        private readonly TaskbarIcon _trayIcon;
        private readonly MainWindow _window;

        public TrayIcon(MainWindow window)
        {
            _window = window;
            _trayIcon = new TaskbarIcon { ToolTipText = "Asset Explorer" };

            try
            {
                var uri = new Uri("pack://application:,,,/icon.ico");
                var decoder = new IconBitmapDecoder(uri,
                    BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                _trayIcon.IconSource = decoder.Frames[0];
            }
            catch { }

            var menu = new System.Windows.Controls.ContextMenu();

            var open = new System.Windows.Controls.MenuItem { Header = "Open" };
            open.Click += (_, __) => ShowWindow();

            // Пункт автозапуску — показує галочку коли увімкнено
            var startup = new System.Windows.Controls.MenuItem();
            startup.Header = "Start with Windows";
            startup.IsCheckable = true;
            startup.IsChecked = StartupManager.IsEnabled();

            startup.Click += (_, __) =>
            {
                if (StartupManager.IsEnabled())
                    StartupManager.Disable();
                else
                    StartupManager.Enable();

                startup.IsChecked = StartupManager.IsEnabled();
            };

            var exit = new System.Windows.Controls.MenuItem { Header = "Exit" };
            exit.Click += (_, __) => System.Windows.Application.Current.Shutdown();

            menu.Items.Add(open);

            menu.Items.Add(new System.Windows.Controls.Separator());

            menu.Items.Add(startup);

            menu.Items.Add(new System.Windows.Controls.Separator());

            menu.Items.Add(exit);

            _trayIcon.ContextMenu = menu;
            _trayIcon.TrayMouseDoubleClick += (_, __) => ShowWindow();
        }

        public void ShowWindow()
        {
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
        }

        public void Dispose() => _trayIcon.Dispose();
    }
}
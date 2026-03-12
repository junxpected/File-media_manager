using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using File_manager.Interfaces;
using File_manager.Models;
using File_manager.Services;
using File_manager.ViewModels;
using Microsoft.Win32;

namespace File_manager.View
{
    public class ColWidthConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
            => value is double w ? Math.Max(0, w - 20) : 0.0;
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
            => System.Windows.DependencyProperty.UnsetValue;
    }

    public class ExtIconConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
        {
            var ext = Path.GetExtension(value as string ?? "").ToLower();
            return ext switch
            {
                ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv" => "🎬",
                ".mp3" or ".wav" or ".flac" or ".aac" or ".ogg" => "🎵",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".svg" => "🖼️",
                ".pdf"  => "📕",
                ".doc" or ".docx" => "📝",
                ".xls" or ".xlsx" => "📊",
                ".ppt" or ".pptx" => "📋",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "📦",
                ".exe" or ".msi" => "⚙️",
                ".cs"  => "🔷",
                ".py"  => "🐍",
                ".js" or ".ts" => "🟨",
                ".html" or ".htm" => "🌐",
                ".css" => "🎨",
                ".json" or ".xml" or ".yaml" or ".yml" => "📄",
                ".txt" or ".md" or ".log" => "📃",
                ".sql" => "🗃️",
                ".psd" or ".ai" => "🎭",
                _ => "📄"
            };
        }
        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
            => System.Windows.DependencyProperty.UnsetValue;
    }

    public partial class MainWindow : Window
    {
        private AssetViewModel _viewModel;
        private CollectionViewSource _viewSource;
        private FileStatus? _statusFilter = null;
        private string _extFilter = "";
        private string _searchQuery = "";
        private IAsset? _selectedAsset = null;
        private const string RecentFile = "recent_folders.txt";
        private ObservableCollection<string> _recentFolders = new();
        private TrayIcon? _tray;

        public MainWindow()
        {
            InitializeComponent();

            var watcher    = new FileSystemMonitor();
            var evaluator  = new StatusEvaluator();
            var repository = new SSVRepository("assets.csv");

            _viewModel = new AssetViewModel(watcher, repository, evaluator);
            _viewSource = new CollectionViewSource { Source = _viewModel.Assets };
            _viewSource.GroupDescriptions.Add(new PropertyGroupDescription("FolderName"));
            _viewSource.Filter += ApplyFilters;

            FileList.ItemsSource = _viewSource.View;
            LoadRecentFolders();
            UpdateStats();
            Loaded += (_, __) => HookColumnWidths();

            _tray = new TrayIcon(this);

            var args = Environment.GetCommandLineArgs();
            if (args.Contains("--minimized")) Hide();

            Closing += (s, e) => { e.Cancel = true; Hide(); };
        }

        // ── OPEN FOLDER ──────────────────────────────────────────────
        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Select folder" };
            if (dialog.ShowDialog() == true)
                OpenFolder(dialog.FolderName);
        }

        public async void OpenFolder(string path)
        {
            BtnOpen.IsEnabled = false;
            StatusBarText.Text = $"Loading: {path}...";

            _viewModel.OnProgress = pct =>
            {
                if (pct == null)
                {
                    LoadingBar.Visibility = Visibility.Collapsed;
                    LoadingBar.Value = 0;
                }
                else
                {
                    LoadingBar.Visibility = Visibility.Visible;
                    LoadingBar.Value = pct.Value;
                    StatusBarText.Text = $"Loading... {pct}%";
                }
            };

            await _viewModel.LoadFolderAsync(path);
            _viewModel.StartWatching(path);

            EmptyState.Visibility = _viewModel.Assets.Count == 0
                ? Visibility.Visible : Visibility.Collapsed;
            RefreshView();

            var projectLabel = _viewModel.CurrentProjectType switch
            {
                File_manager.Services.ProjectType.Unity  => "🎮 Unity",
                File_manager.Services.ProjectType.Unreal => "🎮 Unreal",
                File_manager.Services.ProjectType.Node   => "📦 Node.js",
                File_manager.Services.ProjectType.DotNet => "🔷 .NET",
                File_manager.Services.ProjectType.Python => "🐍 Python",
                _ => "📁 Generic"
            };
            StatusBarText.Text = $"{projectLabel}  |  {path}";
            SaveRecentFolder(path);
            BtnOpen.IsEnabled = true;
        }

        // ── SELECTION ────────────────────────────────────────────────
        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedAsset = FileList.SelectedItem as IAsset;
            if (_selectedAsset == null) return;

            DetailName.Text   = _selectedAsset.Name;
            DetailPath.Text   = _selectedAsset.FullPath;
            DetailStatus.Text = _selectedAsset.Status.ToString();
            CommentBox.Text   = _selectedAsset.Comment ?? "";

            if (_selectedAsset is MediaAsset ma)
            {
                DetailSize.Text   = ma.SizeFormatted;
                DetailFolder.Text = ma.FolderName;
            }

            var ext = Path.GetExtension(_selectedAsset.Name).ToLower();
            DetailIcon.Text = ext switch
            {
                ".mp4" or ".mov" or ".avi" or ".mkv" => "🎬",
                ".mp3" or ".wav" or ".flac"           => "🎵",
                ".jpg" or ".jpeg" or ".png" or ".gif" => "🖼️",
                ".pdf"                                => "📕",
                ".docx" or ".doc"                     => "📝",
                ".zip" or ".rar" or ".7z"             => "📦",
                _ => "📄"
            };
        }

        // ── SEARCH ───────────────────────────────────────────────────
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchQuery = SearchBox.Text.Trim().ToLower();
            RefreshView();
        }

        // ── STATUS FILTERS ───────────────────────────────────────────
        private void FilterAll_Click(object sender, RoutedEventArgs e)      { _statusFilter = null;                RefreshView(); }
        private void FilterNew_Click(object sender, RoutedEventArgs e)      { _statusFilter = FileStatus.New;      RefreshView(); }
        private void FilterModified_Click(object sender, RoutedEventArgs e) { _statusFilter = FileStatus.Modified; RefreshView(); }
        private void FilterMissing_Click(object sender, RoutedEventArgs e)  { _statusFilter = FileStatus.Missing;  RefreshView(); }
        private void FilterApproved_Click(object sender, RoutedEventArgs e) { _statusFilter = FileStatus.Approved; RefreshView(); }
        private void FilterRejected_Click(object sender, RoutedEventArgs e) { _statusFilter = FileStatus.Rejected; RefreshView(); }
        private void FilterDone_Click(object sender, RoutedEventArgs e)     { _statusFilter = FileStatus.Done;     RefreshView(); }

        // ── EXT FILTER ───────────────────────────────────────────────

        // ── FILTER LOGIC ─────────────────────────────────────────────
        private void ApplyFilters(object sender, FilterEventArgs e)
        {
            if (e.Item is not IAsset asset) { e.Accepted = false; return; }

            if (_statusFilter.HasValue && asset.Status != _statusFilter.Value)
            { e.Accepted = false; return; }

            if (!string.IsNullOrEmpty(_extFilter))
            {
                var ext = Path.GetExtension(asset.Name).ToLower();
                if (ext != _extFilter.ToLower()) { e.Accepted = false; return; }
            }

            if (!string.IsNullOrEmpty(_searchQuery))
            {
                if (!asset.Name.ToLower().Contains(_searchQuery) &&
                    !asset.FullPath.ToLower().Contains(_searchQuery))
                { e.Accepted = false; return; }
            }

            e.Accepted = true;
        }

        private void RefreshView()
        {
            _viewSource?.View?.Refresh();
            UpdateStats();
            var count = _viewSource?.View?.Cast<object>().Count() ?? 0;
            StatusBarCount.Text = $"{count} files";
            if (_viewModel != null)
                EmptyState.Visibility = _viewModel.Assets.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── STATS ────────────────────────────────────────────────────
        private void UpdateStats()
        {
            var all = _viewModel.Assets;
            StatsTotal.Text    = all.Count.ToString();
            StatsNew.Text      = all.Count(a => a.Status == FileStatus.New).ToString();
            StatsModified.Text = all.Count(a => a.Status == FileStatus.Modified).ToString();
            StatsMissing.Text  = all.Count(a => a.Status == FileStatus.Missing).ToString();
            StatsApproved.Text = all.Count(a => a.Status == FileStatus.Approved).ToString();
            StatsDone.Text     = all.Count(a => a.Status == FileStatus.Done).ToString();
        }

        // ── ACTIONS ──────────────────────────────────────────────────


        private void BtnSaveComment_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            _selectedAsset.Comment = CommentBox.Text;
            _viewModel.SaveAndCommit();
            StatusBarText.Text = $"Saved: {_selectedAsset.Name}";
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;

            if (_selectedAsset.Status == FileStatus.Modified)
            {
                var warn = MessageBox.Show(
                    $"This file has unsaved changes (Modified):\n{_selectedAsset.Name}\n\nDelete anyway?",
                    "Warning", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (warn != MessageBoxResult.Yes) return;
            }
            else
            {
                var res = MessageBox.Show(
                    $"Delete file?\n{_selectedAsset.Name}",
                    "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res != MessageBoxResult.Yes) return;
            }

            try
            {
                File.Delete(_selectedAsset.FullPath);
                _viewModel.DeleteAsset(_selectedAsset);
                _selectedAsset = null;
                DetailName.Text = "Select a file";
                DetailPath.Text = DetailSize.Text = DetailStatus.Text = DetailFolder.Text = "—";
                DetailIcon.Text = "📄";
                CommentBox.Text = "";
                RefreshView();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Delete failed"); }
        }

        // ── DOUBLE CLICK ─────────────────────────────────────────────
        private void FileList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_selectedAsset == null) return;
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _selectedAsset.FullPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex) { StatusBarText.Text = $"Cannot open: {ex.Message}"; }
        }

        // ── RECENT FOLDERS ───────────────────────────────────────────
        private void LoadRecentFolders()
        {
            _recentFolders.Clear();
            if (File.Exists(RecentFile))
                foreach (var line in File.ReadAllLines(RecentFile))
                    if (Directory.Exists(line) && !_recentFolders.Contains(line))
                        _recentFolders.Add(line);
            RecentList.ItemsSource = _recentFolders;
        }

        private void SaveRecentFolder(string path)
        {
            if (_recentFolders.Contains(path)) _recentFolders.Remove(path);
            _recentFolders.Insert(0, path);
            while (_recentFolders.Count > 8) _recentFolders.RemoveAt(_recentFolders.Count - 1);
            File.WriteAllLines(RecentFile, _recentFolders);
            RecentList.ItemsSource = null;
            RecentList.ItemsSource = _recentFolders;
        }

        private void RecentFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string path && Directory.Exists(path))
                OpenFolder(path);
        }

        // ── COLUMN WIDTH TRACKING ────────────────────────────────────
        private void HookColumnWidths()
        {
            var gv = (GridView)FileList.View;
            var dpd = System.ComponentModel.DependencyPropertyDescriptor
                      .FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn));
            foreach (var col in gv.Columns)
            {
                var c = col;
                dpd.AddValueChanged(c, (_, __) => UpdateColumnTags(gv));
            }
            UpdateColumnTags(gv);
        }

        private void UpdateColumnTags(GridView gv)
        {
            if (gv.Columns.Count > 1)
                FileList.Tag = Math.Max(0, gv.Columns[1].Width - 16);
        }

        // ── CONTEXT MENU ─────────────────────────────────────────────
        private void FileList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(
                FileList, e.OriginalSource as DependencyObject) as ListViewItem;
            if (item != null) { item.IsSelected = true; e.Handled = false; }
        }
        private void Ctx_Open(object sender, RoutedEventArgs e)
            => FileList_DoubleClick(sender, null!);

        private void Ctx_CopyPath(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            Clipboard.SetText(_selectedAsset.FullPath);
            StatusBarText.Text = "Path copied to clipboard";
        }

        private void Ctx_OpenInExplorer(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            Process.Start("explorer.exe", $"/select,\"{_selectedAsset.FullPath}\"");
        }

        private void Ctx_Rename(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            var dialog = new RenameDialog(_selectedAsset.Name) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var dir     = Path.GetDirectoryName(_selectedAsset.FullPath)!;
                    var newPath = Path.Combine(dir, dialog.NewName);
                    File.Move(_selectedAsset.FullPath, newPath);
                    _selectedAsset.FullPath = newPath;
                    _viewModel.SaveAndCommit();
                    StatusBarText.Text = $"Renamed to {dialog.NewName}";
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Rename failed"); }
            }
        }

        private void Ctx_Delete(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            var res = MessageBox.Show(
                $"Delete file?\n{_selectedAsset.Name}",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (res != MessageBoxResult.Yes) return;
            try
            {
                File.Delete(_selectedAsset.FullPath);
                _viewModel.DeleteAsset(_selectedAsset);
                _selectedAsset = null;
                StatusBarText.Text = "File deleted";
            }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Delete failed"); }
        }

        private void FileList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.F2)
                Ctx_Rename(sender, new RoutedEventArgs());
            else if (e.Key == System.Windows.Input.Key.Delete)
                Ctx_Delete(sender, new RoutedEventArgs());
        }

        private void ExtFilter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_viewSource == null) return;
            var val = ExtFilter.Text.Trim().ToLower();
            if (val.Length > 0 && !val.StartsWith("."))
                val = "." + val;
            _extFilter = val;
            RefreshView();
        }

        private void BtnApprove_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            _viewModel.UpdateBaseline(_selectedAsset);
            _selectedAsset.Status = FileStatus.Approved;
            DetailStatus.Text = _selectedAsset.Status.ToString();
            _viewModel.SaveAndCommit();
            RefreshView();
            StatusBarText.Text = $"Approved: {_selectedAsset.Name}";
        }

        private void BtnReject_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            _viewModel.UpdateBaseline(_selectedAsset);
            _selectedAsset.Status = FileStatus.Rejected;
            DetailStatus.Text = _selectedAsset.Status.ToString();
            _viewModel.SaveAndCommit();
            RefreshView();
            StatusBarText.Text = $"Rejected: {_selectedAsset.Name}";
        }

        private void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            _viewModel.UpdateBaseline(_selectedAsset);
            _selectedAsset.Status = FileStatus.Done;
            DetailStatus.Text = _selectedAsset.Status.ToString();
            _viewModel.SaveAndCommit();
            RefreshView();
            StatusBarText.Text = $"Done: {_selectedAsset.Name}";
        }

        private void RecentFolder_NewWindow_Click(object sender, RoutedEventArgs e)
        {
            var path = (sender as MenuItem)?.Tag as string;
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            var newWindow = new MainWindow();
            newWindow.Show();
            newWindow.OpenFolder(path);
        }

        private void RecentFolder_Remove_Click(object sender, RoutedEventArgs e)
        {
            var path = (sender as MenuItem)?.Tag as string;
            if (string.IsNullOrEmpty(path)) return;
            _recentFolders.Remove(path);
            File.WriteAllLines(RecentFile, _recentFolders);
            RecentList.ItemsSource = null;
            RecentList.ItemsSource = _recentFolders;
        }
    }
}
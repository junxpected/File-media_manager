using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using File_manager.Interfaces;
using File_manager.Models;
using File_manager.Services;
using File_manager.ViewModels;
using Microsoft.Win32;

namespace File_manager.View
{
    public partial class MainWindow : Window
    {
        private AssetViewModel _viewModel;
        private CollectionViewSource _viewSource;

        private FileStatus? _statusFilter = null;
        private string _extFilter = "";
        private bool _showHidden = false;
        private string _searchQuery = "";
        private IAsset? _selectedAsset = null;

        public MainWindow()
        {
            InitializeComponent();

            var watcher = new FileSystemMonitor();
            var evaluator = new StatusEvaluator();
            var repository = new SSVRepository("assets.csv");

            _viewModel = new AssetViewModel(watcher, repository, evaluator);

            _viewSource = new CollectionViewSource { Source = _viewModel.Assets };
            _viewSource.GroupDescriptions.Add(new PropertyGroupDescription("FolderName"));
            _viewSource.Filter += ApplyFilters;

            FileList.ItemsSource = _viewSource.View;
            UpdateStats();
        }

        // ─────────────────────────── OPEN FOLDER ──
        private void BtnOpen_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog { Title = "Select media folder" };
            if (dialog.ShowDialog() == true)
            {
                var path = dialog.FolderName;
                _viewModel.LoadFolder(path);
                _viewModel.StartWatching(path);

                EmptyState.Visibility = _viewModel.Assets.Count == 0
                    ? Visibility.Visible : Visibility.Collapsed;

                RefreshView();
                StatusBarText.Text = $"Loaded: {path}";
            }
        }

        // ─────────────────────────── SELECTION ──
        private void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedAsset = FileList.SelectedItem as IAsset;
            if (_selectedAsset == null) return;

            DetailName.Text = _selectedAsset.Name;
            DetailPath.Text = _selectedAsset.FullPath;
            DetailStatus.Text = _selectedAsset.Status.ToString();
            CommentBox.Text = _selectedAsset.Comment ?? "";

            if (_selectedAsset is MediaAsset ma)
            {
                DetailSize.Text = ma.SizeFormatted;
                DetailFolder.Text = ma.FolderName;
            }

            var ext = Path.GetExtension(_selectedAsset.Name).ToLower();
            DetailIcon.Text = ext switch
            {
                ".mp4" or ".mov" or ".avi" or ".mkv" => "🎬",
                ".mp3" or ".wav" or ".flac" => "🎵",
                ".jpg" or ".jpeg" or ".png" or ".gif" => "🖼",
                ".pdf" => "📕",
                ".docx" or ".doc" => "📝",
                ".zip" or ".rar" or ".7z" => "🗜",
                _ => "📄"
            };
        }

        // ─────────────────────────── SEARCH ──
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchQuery = SearchBox.Text.Trim().ToLower();
            RefreshView();
        }

        // ─────────────────────────── STATUS FILTERS ──
        private void FilterAll_Click(object sender, RoutedEventArgs e) { _statusFilter = null; RefreshView(); }
        private void FilterNew_Click(object sender, RoutedEventArgs e) { _statusFilter = FileStatus.New; RefreshView(); }
        private void FilterModified_Click(object sender, RoutedEventArgs e) { _statusFilter = FileStatus.Modified; RefreshView(); }
        private void FilterMissing_Click(object sender, RoutedEventArgs e) { _statusFilter = FileStatus.Missing; RefreshView(); }
        private void FilterApproved_Click(object sender, RoutedEventArgs e) { _statusFilter = FileStatus.Approved; RefreshView(); }
        private void FilterRejected_Click(object sender, RoutedEventArgs e) { _statusFilter = FileStatus.Rejected; RefreshView(); }
        private void FilterDone_Click(object sender, RoutedEventArgs e) { _statusFilter = FileStatus.Done; RefreshView(); }

        // ─────────────────────────── EXT FILTER ──
        private void ExtFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewSource == null) return;
            if (ExtFilter.SelectedItem is ComboBoxItem item)
            {
                var val = item.Content?.ToString() ?? "All types";
                _extFilter = val == "All types" ? "" : val;
                RefreshView();
            }
        }

        // ─────────────────────────── SHOW HIDDEN ──
        private void ShowHidden_Changed(object sender, RoutedEventArgs e)
        {
            _showHidden = ShowHidden.IsChecked == true;
            RefreshView();
        }

        // ─────────────────────────── FILTER LOGIC ──
        private bool IsHiddenPath(string fullPath)
        {
            try
            {
                // Перевіряємо кожен сегмент шляху: єсли папка/файл починається з . — це ихований
                var parts = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (var part in parts)
                {
                    if (string.IsNullOrEmpty(part)) continue;
                    if (part.Length == 2 && part[1] == ':') continue; // skip drive C:
                    if (part.StartsWith(".")) return true;
                }

                // Windows Hidden атрибут тільки самого файлу (не батьківських папок!)
                var info = new FileInfo(fullPath);
                if (info.Exists && info.Attributes.HasFlag(FileAttributes.Hidden))
                    return true;
            }
            catch { }
            return false;
        }

        private void ApplyFilters(object sender, FilterEventArgs e)
        {
            if (e.Item is not IAsset asset) { e.Accepted = false; return; }

            // Hidden files AND hidden parent folders
            if (!_showHidden && IsHiddenPath(asset.FullPath))
            { e.Accepted = false; return; }

            // Status
            if (_statusFilter.HasValue && asset.Status != _statusFilter.Value)
            { e.Accepted = false; return; }

            // Extension
            if (!string.IsNullOrEmpty(_extFilter))
            {
                var ext = Path.GetExtension(asset.Name).ToLower();
                if (ext != _extFilter.ToLower()) { e.Accepted = false; return; }
            }

            // Search
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

        // ─────────────────────────── STATS ──
        private void UpdateStats()
        {
            var all = _viewModel.Assets;
            StatsTotal.Text = all.Count.ToString();
            StatsNew.Text = all.Count(a => a.Status == FileStatus.New).ToString();
            StatsModified.Text = all.Count(a => a.Status == FileStatus.Modified).ToString();
            StatsMissing.Text = all.Count(a => a.Status == FileStatus.Missing).ToString();
            StatsApproved.Text = all.Count(a => a.Status == FileStatus.Approved).ToString();
            StatsDone.Text = all.Count(a => a.Status == FileStatus.Done).ToString();
        }

        // ─────────────────────────── ACTIONS ──
        private void BtnApprove_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            _selectedAsset.Status = FileStatus.Approved;
            DetailStatus.Text = _selectedAsset.Status.ToString();
            _viewModel.SaveAndCommit();
            RefreshView();
            StatusBarText.Text = $"Approved: {_selectedAsset.Name}";
        }

        private void BtnReject_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            _selectedAsset.Status = FileStatus.Rejected;
            DetailStatus.Text = _selectedAsset.Status.ToString();
            _viewModel.SaveAndCommit();
            RefreshView();
            StatusBarText.Text = $"Rejected: {_selectedAsset.Name}";
        }

        private void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            _selectedAsset.Status = FileStatus.Done;
            DetailStatus.Text = _selectedAsset.Status.ToString();
            _viewModel.SaveAndCommit();
            RefreshView();
            StatusBarText.Text = $"Done: {_selectedAsset.Name}";
        }

        private void BtnSaveComment_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            _selectedAsset.Comment = CommentBox.Text;
            _viewModel.SaveAndCommit(); // зберігаємо в CSV
            FileList.Items.Refresh();
            StatusBarText.Text = $"Збережено: {_selectedAsset.Name}";
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAsset == null) return;
            _viewModel.DeleteAsset(_selectedAsset);
            _selectedAsset = null;

            DetailName.Text = "Select a file";
            DetailPath.Text = "—";
            DetailSize.Text = "—";
            DetailStatus.Text = "—";
            DetailFolder.Text = "—";
            DetailIcon.Text = "📄";
            CommentBox.Text = "";

            RefreshView();
        }
    }
}
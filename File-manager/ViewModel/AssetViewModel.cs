using File_manager.Interfaces;
using File_manager.Models;
using File_manager.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace File_manager.ViewModels
{
    public class AssetViewModel
    {
        private readonly IFileWatcher _watcher;
        private readonly IAssetRepository _repository; // залежність від інтерфейсу, не від класу
        private readonly IStatusEvaluator _evaluator;
        private readonly FswDebouncer _debouncer = new(); // S: дебаунс в окремому класі

        public ObservableCollection<IAsset> Assets { get; } = new();
        public ProjectType CurrentProjectType { get; private set; } = ProjectType.Generic;
        public Action<int?>? OnProgress { get; set; }

        private CancellationTokenSource? _loadCts;

        public AssetViewModel(IFileWatcher watcher, IAssetRepository repository, IStatusEvaluator evaluator)
        {
            _watcher = watcher;
            _repository = repository;
            _evaluator = evaluator;
            _watcher.OnFileSystemChanged += HandleFileChange;
        }

        public void StartWatching(string path) => _watcher.Start(path);

        public async Task LoadFolderAsync(string path)
        {
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var ct = _loadCts.Token;

            Assets.Clear();
            OnProgress?.Invoke(0);

            CurrentProjectType = await Task.Run(() => IgnoreRules.DetectProjectType(path), ct);

            Dictionary<string, IAsset> saved;
            try
            {
                saved = await Task.Run(() => _repository.LoadAsDictionary(), ct);
            }
            catch (OperationCanceledException) { OnProgress?.Invoke(null); return; }

            List<string> files;
            try
            {
                files = await Task.Run(() =>
                    IgnoreRules.GetFiles(path, CurrentProjectType, ct).ToList(), ct);
            }
            catch (OperationCanceledException)
            {
                OnProgress?.Invoke(null);
                return;
            }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                    MessageBox.Show($"Cannot read folder:\n{ex.Message}", "Error"));
                OnProgress?.Invoke(null);
                return;
            }

            var total = files.Count;
            var batch = new List<MediaAsset>(250);
            int processed = 0;

            foreach (var filePath in files)
            {
                if (ct.IsCancellationRequested) { OnProgress?.Invoke(null); return; }

                batch.Add(BuildAsset(filePath, saved));
                processed++;

                if (batch.Count >= 100 || processed == total)
                {
                    var toAdd = batch.ToList();
                    batch.Clear();

                    // Спочатку зберігаємо на диск — в фоновому потоці
                    await Task.Run(() =>
                    {
                        foreach (var a in toAdd)
                            _repository.Save(a);
                    }, ct);

                    // Потім додаємо в UI
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        foreach (var a in toAdd)
                            Assets.Add(a);
                        OnProgress?.Invoke(total > 0 ? processed * 100 / total : 100);
                    });

                    await Task.Delay(5, ct).ContinueWith(_ => { });
                }
            }

            _repository.Commit();
            OnProgress?.Invoke(null);
        }

        private MediaAsset BuildAsset(string filePath, Dictionary<string, IAsset> saved)
        {
            if (saved.TryGetValue(filePath, out var existing) && existing is MediaAsset asset)
            {
                asset.Status = _evaluator.ResolveStatus(new FileInfo(filePath), asset.Baseline, asset.Status);
                return asset;
            }

            var fi = new FileInfo(filePath);
            var baseline = new AssetMetadata
            {
                RegisteredTime = fi.Exists ? fi.LastWriteTime : DateTime.Now,
                RegisteredSize = fi.Exists ? fi.Length : 0,
                FirstSeenTime = fi.Exists ? fi.CreationTime : DateTime.Now
            };
            return new MediaAsset
            {
                FullPath = filePath,
                Baseline = baseline,
                Status = _evaluator.CalculateStatus(fi, baseline),
                Comment = string.Empty
            };
        }

        private void HandleFileChange(FileSystemEventArgs e)
        {
            _debouncer.Debounce(e.FullPath, () => ProcessFileChange(e));
        }

        

        private static readonly string[] _systemExtensions = { ".pek", ".tmp", ".bak", ".cache" };

        private bool TryHandleSystemFile(string fullPath)
        {
            var ext = Path.GetExtension(fullPath).ToLower();
            if (!_systemExtensions.Contains(ext)) return false;

            // IMG_4435.MOV 48000.pek → шукаємо IMG_4435.MOV
            var fileName = Path.GetFileName(fullPath);
            var parentName = fileName.Split(' ')[0]; // "IMG_4435.MOV"
            
            var parent = Assets.FirstOrDefault(a => 
                Path.GetFileName(a.FullPath).Equals(parentName, 
                StringComparison.OrdinalIgnoreCase));

            if (parent != null && parent.Status == FileStatus.New)
            {
                parent.Status = FileStatus.Modified;
                _repository.Save(parent);
                _repository.Commit();
            }

            return true;
        }
        private void ProcessFileChange(FileSystemEventArgs e)
        {
        if (IgnoreRules.ShouldIgnoreFile(Path.GetFileName(e.FullPath))) return;
        var dirName = Path.GetFileName(Path.GetDirectoryName(e.FullPath) ?? "");
        if (IgnoreRules.ShouldIgnoreDirectory(dirName, CurrentProjectType)) return;

        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
        {
            try
            {
                if (TryHandleSystemFile(e.FullPath)) return;

                var existing = Assets.FirstOrDefault(a => a.FullPath == e.FullPath);

                if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    if (existing == null) return;
                    if (existing.Status == FileStatus.New)
                        Assets.Remove(existing);
                    else
                        existing.Status = FileStatus.Missing;
                    _repository.Save(existing);
                    _repository.Commit();
                    return;
                }

                if (existing == null)
                {
                    var info = new FileInfo(e.FullPath);
                    if (!info.Exists) return;
                    var baseline = new AssetMetadata
                    {
                        RegisteredTime = info.LastWriteTime,
                        RegisteredSize = info.Length,
                        FirstSeenTime = DateTime.Now
                    };
                    var newAsset = new MediaAsset
                    {
                        FullPath = e.FullPath,
                        Baseline = baseline,
                        Status = FileStatus.New,
                        Comment = string.Empty
                    };
                    Assets.Add(newAsset);
                    _repository.Save(newAsset);
                    _repository.Commit();
                }
                else
                {
                    var info = new FileInfo(e.FullPath);
                    if (!info.Exists)
                        existing.Status = FileStatus.Missing;
                    else
                        existing.Status = _evaluator.ResolveStatus(
                            info, existing.Baseline, existing.Status);
                    _repository.Save(existing);
                    _repository.Commit();
                }
            }
            catch { }
        });
    }

        public void UpdateBaseline(IAsset asset)
        {
            var info = new FileInfo(asset.FullPath);
            if (!info.Exists)
                return;
            asset.Baseline.RegisteredTime = info.LastWriteTime;
            asset.Baseline.RegisteredSize = info.Length;
        } // Оновлює baseline при ручному підтвердженні (Approve/Reject/Done)

        public void SaveAndCommit() => _repository.Commit();

        public void DeleteAsset(IAsset asset)
        {
            Assets.Remove(asset);
            _repository.Remove(asset);
            _repository.Commit();
        }
    }
}
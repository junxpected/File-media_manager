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
        private readonly SSVRepository _repository;
        private readonly IStatusEvaluator _evaluator;

        public ObservableCollection<IAsset> Assets { get; } = new();
        public ProjectType CurrentProjectType { get; private set; } = ProjectType.Generic;

        public Action<int?>? OnProgress { get; set; }

        private readonly Dictionary<string, System.Timers.Timer> _fswDebounce = new();
        private readonly object _fswLock = new();
        private CancellationTokenSource? _loadCts;

        public AssetViewModel(IFileWatcher watcher, SSVRepository repository, IStatusEvaluator evaluator)
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
            try { saved = await Task.Run(() => _repository.LoadAsDictionary(), ct); }
            catch (OperationCanceledException) { OnProgress?.Invoke(null); return; }

            List<string> files;
            try
            {
                files = await Task.Run(() =>
                    IgnoreRules.GetFiles(path, CurrentProjectType, ct).ToList(), ct);
            }
            catch (OperationCanceledException) { OnProgress?.Invoke(null); return; }
            catch (Exception ex)
            {
                Application.Current?.Dispatcher.Invoke(() =>
                    MessageBox.Show($"Cannot read folder:\n{ex.Message}", "Error"));
                OnProgress?.Invoke(null);
                return;
            }

            var total = files.Count;
            var batch = new List<MediaAsset>(100);
            int processed = 0;

            foreach (var filePath in files)
            {
                if (ct.IsCancellationRequested) { OnProgress?.Invoke(null); return; }

                var asset = BuildAsset(filePath, saved);
                batch.Add(asset);
                processed++;

                if (batch.Count >= 100 || processed == total)
                {
                    var toAdd = batch.ToList();
                    batch.Clear();

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        foreach (var a in toAdd)
                        {
                            Assets.Add(a);
                            _repository.Save(a);
                        }
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
                var info = new FileInfo(filePath);

                // Тільки перевіряємо Missing і Modified
                // Baseline НЕ оновлюємо тут — тільки коли юзер явно підтверджує (Approve/Done)
                asset.Status = _evaluator.ResolveStatus(info, asset.Baseline, asset.Status);
                return asset;
            }

            // Новий файл — перша поява
            var fi = new FileInfo(filePath);
            var baseline = new AssetMetadata
            {
                RegisteredTime = fi.Exists ? fi.LastWriteTime : DateTime.Now,
                RegisteredSize = fi.Exists ? fi.Length : 0,
                FirstSeenTime  = fi.Exists ? fi.CreationTime : DateTime.Now
            };

            return new MediaAsset
            {
                FullPath = filePath,
                Baseline = baseline,
                Status   = _evaluator.CalculateStatus(fi, baseline),
                Comment  = string.Empty
            };
        }

        // Викликати з MainWindow після Approve/Done щоб оновити baseline
        public void UpdateBaseline(IAsset asset)
        {
            var info = new FileInfo(asset.FullPath);
            if (!info.Exists) return;

            asset.Baseline.RegisteredTime = info.LastWriteTime;
            asset.Baseline.RegisteredSize = info.Length;
        }

        private void HandleFileChange(FileSystemEventArgs e)
        {
            lock (_fswLock)
            {
                if (_fswDebounce.TryGetValue(e.FullPath, out var old))
                {
                    old.Stop();
                    old.Dispose();
                }
                var timer = new System.Timers.Timer(500) { AutoReset = false };
                timer.Elapsed += (_, __) => ProcessFileChange(e);
                _fswDebounce[e.FullPath] = timer;
                timer.Start();
            }
        }

        private void ProcessFileChange(FileSystemEventArgs e)
        {
            lock (_fswLock)
            {
                if (_fswDebounce.TryGetValue(e.FullPath, out var t))
                {
                    t.Dispose();
                    _fswDebounce.Remove(e.FullPath);
                }
            }

            if (IgnoreRules.ShouldIgnoreFile(Path.GetFileName(e.FullPath))) return;
            var dirName = Path.GetFileName(Path.GetDirectoryName(e.FullPath) ?? "");
            if (IgnoreRules.ShouldIgnoreDirectory(dirName, CurrentProjectType)) return;

            Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, () =>
            {
                try
                {
                    var existing = Assets.FirstOrDefault(a => a.FullPath == e.FullPath);

                    if (existing == null)
                    {
                        var info = new FileInfo(e.FullPath);
                        if (!info.Exists) return;

                        var baseline = new AssetMetadata
                        {
                            RegisteredTime = info.LastWriteTime,
                            RegisteredSize = info.Length,
                            FirstSeenTime  = DateTime.Now
                        };
                        var newAsset = new MediaAsset
                        {
                            FullPath = e.FullPath,
                            Baseline = baseline,
                            Status   = FileStatus.New,
                            Comment  = string.Empty
                        };
                        Assets.Add(newAsset);
                        _repository.Save(newAsset);
                        _repository.Commit();
                    }
                    else
                    {
                        var info = new FileInfo(e.FullPath);
                        // Baseline не оновлюємо — тільки показуємо поточний стан
                        existing.Status = _evaluator.ResolveStatus(info, existing.Baseline, existing.Status);
                        _repository.Commit();
                    }
                }
                catch { }
            });
        }

        public void SaveAndCommit() => _repository.Commit();

        public void DeleteAsset(IAsset asset)
        {
            Assets.Remove(asset);
            _repository.Remove(asset);
            _repository.Commit();
        }
    }
}
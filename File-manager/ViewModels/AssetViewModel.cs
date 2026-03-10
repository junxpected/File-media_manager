using File_manager.Interfaces;
using File_manager.Models;
using File_manager.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace File_manager.ViewModels
{
    public class AssetViewModel
    {
        private readonly IFileWatcher _watcher;
        private readonly SSVRepository _repository;
        private readonly IStatusEvaluator _evaluator;

        public ObservableCollection<IAsset> Assets { get; } = new();

        public AssetViewModel(IFileWatcher watcher, SSVRepository repository, IStatusEvaluator evaluator)
        {
            _watcher = watcher;
            _repository = repository;
            _evaluator = evaluator;
            _watcher.OnFileSystemChanged += HandleFileChange;
        }

        public void StartWatching(string path) => _watcher.Start(path);

        public void LoadFolder(string path)
        {
            Assets.Clear();

            // Завантажуємо збережені дані з CSV (коментарі, статуси, дати реєстрації)
            var saved = _repository.LoadAsDictionary();

            foreach (var filePath in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                MediaAsset asset;

                if (saved.TryGetValue(filePath, out var existing) && existing is MediaAsset savedAsset)
                {
                    // Файл вже відомий — відновлюємо збережені дані
                    asset = savedAsset;

                    // Якщо статус був вручну встановлений (Approved/Rejected/Done) — не перераховуємо
                    if (asset.Status == FileStatus.Approved ||
                        asset.Status == FileStatus.Rejected ||
                        asset.Status == FileStatus.Done)
                    {
                        // залишаємо статус як є
                    }
                    else
                    {
                        // Перераховуємо автоматичний статус
                        var info = new FileInfo(filePath);
                        asset.Status = _evaluator.CalculateStatus(info, asset.Baseline);
                        // Оновлюємо baseline якщо файл змінився (щоб наступний запуск теж бачив зміну)
                        // НЕ оновлюємо — зберігаємо оригінальний стан для відстеження
                    }
                }
                else
                {
                    // Новий файл — реєструємо вперше
                    var info = new FileInfo(filePath);
                    asset = new MediaAsset
                    {
                        FullPath = filePath,
                        Baseline = new AssetMetadata
                        {
                            // RegisteredTime = час модифікації файлу (для порівняння змін)
                            // RegisteredSize  = розмір файлу (для порівняння змін)
                            // Для визначення "New" використовуємо окремо: час першої реєстрації = DateTime.Now
                            RegisteredTime = info.Exists ? info.LastWriteTime : DateTime.Now,
                            RegisteredSize = info.Exists ? info.Length : 0,
                            FirstSeenTime = DateTime.Now
                        },
                        Status = FileStatus.New,
                        Comment = string.Empty
                    };
                }

                Assets.Add(asset);
                _repository.Save(asset);
            }

            _repository.Commit();
        }

        private void HandleFileChange(FileSystemEventArgs e)
        {
            var existing = Assets.FirstOrDefault(a => a.FullPath == e.FullPath);
            if (existing == null)
            {
                var info = new FileInfo(e.FullPath);
                var newAsset = new MediaAsset
                {
                    FullPath = e.FullPath,
                    Baseline = new AssetMetadata
                    {
                        RegisteredTime = DateTime.Now,
                        RegisteredSize = info.Exists ? info.Length : 0
                    },
                    Status = FileStatus.New
                };
                Assets.Add(newAsset);
                _repository.Save(newAsset);
                _repository.Commit();
            }
            else
            {
                var info = new FileInfo(e.FullPath);
                existing.Status = _evaluator.CalculateStatus(info, existing.Baseline);
                _repository.Commit();
            }
        }

        public void SaveAndCommit() => _repository.Commit();

        public void DeleteAsset(IAsset asset)
        {
            if (asset.Status == FileStatus.New)
            {
                var result = MessageBox.Show(
                    $"Видалити файл зі списку?\n{asset.FullPath}",
                    "Підтвердження",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    Assets.Remove(asset);
                    _repository.Commit();
                }
            }
            else
            {
                MessageBox.Show($"Файл має статус '{asset.Status}' і не може бути видалений.");
            }
        }
    }
}
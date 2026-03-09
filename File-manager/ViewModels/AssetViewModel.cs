using File_manager.Interfaces;
using File_manager.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shapes;


namespace File_manager.ViewModels
{
    public class AssetViewModel
    {
        private readonly IFileWatcher _watcher;
        private readonly IRepository<IAsset> _repository;
        private readonly IStatusEvaluator _evaluator;


        public ObservableCollection<IAsset> Assets { get; } = new ();
        public ICommand? ApproveCommand { get;  }
        public ICommand? RejectCommand { get;  }
        public ICommand? MarkDoneCommand { get;  }



        public AssetViewModel(IFileWatcher watcher, IRepository<IAsset> repository, IStatusEvaluator evaluator)
        {
            _watcher = watcher;
            _repository = repository;        
            _evaluator = evaluator;

            _watcher.OnFileSystemChanged += HandleFileChange;
        }  // для ініціалізації полів

        private void HandleFileChange(FileSystemEventArgs e)
        {
            var existing = Assets.FirstOrDefault(a => a.FullPath == e.FullPath);
            if (existing == null)
            {
                var newAsset = new MediaAsset { FullPath = e.FullPath };
                Assets.Add(newAsset);
            }
            else
            {
                var info = new FileInfo(e.FullPath);
                existing.Status = _evaluator.CalculateStatus(info, existing.Baseline);
            }
        }

        public void DeleteAsset(IAsset asset)
        {
            if (asset.Status == FileStatus.New)
            {
                var result = MessageBox.Show($"Ви Впевнені що хочете видалити файл? {asset.FullPath}?", "Підтвередження", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                Assets.Remove(asset);
            }
            else
            {
                MessageBox.Show($"файл має статус: {asset.Status} і не може бути видалений");
            }
        }
        public void StartWatching(string path)
        {
            _watcher.Start(path);
        }
        public void LoadFolder(string path)
        {
            Assets.Clear();
            foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            {
                var asset = new MediaAsset { FullPath = file };
                Assets.Add(asset);
                _repository.Save(asset);
            }
            _repository.Commit();
        }
    }
}

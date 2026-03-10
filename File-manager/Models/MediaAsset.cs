using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using File_manager.Interfaces;

namespace File_manager.Models
{
    public class MediaAsset : IAsset, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public Guid Id { get; set; } = Guid.NewGuid();

        private string _fullPath = string.Empty;
        public string FullPath
        {
            get => _fullPath;
            set { _fullPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(Name)); OnPropertyChanged(nameof(FolderName)); OnPropertyChanged(nameof(SizeFormatted)); }
        }

        private FileStatus _status = FileStatus.New;
        public FileStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        private string _comment = string.Empty;
        public string Comment
        {
            get => _comment;
            set { _comment = value; OnPropertyChanged(); }
        }

        public AssetMetadata Baseline { get; set; } = new();

        public string Name => Path.GetFileName(FullPath);
        public string FolderName => Path.GetFileName(Path.GetDirectoryName(FullPath) ?? "") ?? "";

        public string SizeFormatted
        {
            get
            {
                var info = new FileInfo(FullPath);
                if (!info.Exists) return "-";
                var kb = info.Length / 1024.0;
                return kb < 1024 ? $"{kb:F2} KB" : $"{kb / 1024:F2} MB";
            }
        }
    }
}
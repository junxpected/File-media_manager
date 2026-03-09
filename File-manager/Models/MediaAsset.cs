using System.IO;
using File_manager.Models;
using File_manager.Interfaces;

namespace File_manager.Models
{
    public class MediaAsset : IAsset
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string FullPath { get; set; } = string.Empty;
        public string Name => Path.GetFileName(FullPath);
        public FileStatus Status { get; set; } = FileStatus.New;
        public AssetMetadata Baseline { get; set; } = new();
        public string Comment { get; set; } = string.Empty;

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
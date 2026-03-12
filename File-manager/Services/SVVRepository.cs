using File_manager.Interfaces;
using File_manager.Models;
using System.IO;
using System.Linq;

namespace File_manager.Services
{
    // Реалізує IAssetRepository — зберігає всі папки в одному CSV
    public class SSVRepository : IAssetRepository
    {
        private readonly string _filePath;
        private readonly Dictionary<string, IAsset> _cache =
            new(StringComparer.OrdinalIgnoreCase);
        private bool _csvLoaded = false;

        public SSVRepository(string filePath)
        {
            _filePath = filePath;
        }

        private void EnsureLoaded()
        {
            if (_csvLoaded) return;
            _csvLoaded = true;

            if (!File.Exists(_filePath)) return;

            foreach (var line in File.ReadAllLines(_filePath))
            {
                var parts = line.Split(';');
                if (parts.Length < 7) continue;
                try
                {
                    var asset = new MediaAsset
                    {
                        Id       = Guid.Parse(parts[0]),
                        FullPath = parts[1],
                        Comment  = parts[3],
                        Status   = Enum.Parse<FileStatus>(parts[4]),
                        Baseline = new AssetMetadata
                        {
                            RegisteredTime = DateTime.Parse(parts[5]),
                            RegisteredSize = long.Parse(parts[6]),
                            FirstSeenTime  = parts.Length >= 8
                                ? DateTime.Parse(parts[7])
                                : DateTime.Parse(parts[5])
                        }
                    };
                    _cache[asset.FullPath] = asset;
                }
                catch { }
            }
        }

        public Dictionary<string, IAsset> LoadAsDictionary()
        {
            EnsureLoaded();
            return _cache;
        }

        public void Save(IAsset item)
        {
            EnsureLoaded();
            _cache[item.FullPath] = item;
        }

        public void Remove(IAsset item) => _cache.Remove(item.FullPath);

        public IEnumerable<IAsset> LoadAll() => _cache.Values;

        public void Commit()
        {
            var lines = _cache.Values.Select(a =>
                $"{a.Id};{a.FullPath};{a.Name};{a.Comment};{a.Status};" +
                $"{a.Baseline.RegisteredTime:O};{a.Baseline.RegisteredSize};" +
                $"{a.Baseline.FirstSeenTime:O}");
            File.WriteAllLines(_filePath, lines);
        }
    }
}
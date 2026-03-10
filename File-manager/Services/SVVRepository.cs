using File_manager.Interfaces;
using File_manager.Models;
using System.IO;
using System.Linq;

namespace File_manager.Services
{
    public class SSVRepository : IRepository<IAsset>
    {
        private readonly string _filePath;
        private readonly List<IAsset> _cache = new();

        public SSVRepository(string filePath)
        {
            _filePath = filePath;
        }

        public void Save(IAsset item)
        {
            if (!_cache.Any(a => a.Id == item.Id))
                _cache.Add(item);
        }

        /// <summary>Повертає словник FullPath -> IAsset для швидкого відновлення даних</summary>
        public Dictionary<string, IAsset> LoadAsDictionary()
        {
            var dict = new Dictionary<string, IAsset>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(_filePath)) return dict;

            foreach (var line in File.ReadAllLines(_filePath))
            {
                var parts = line.Split(';');
                if (parts.Length < 7) continue;
                try
                {
                    var firstSeen = parts.Length >= 8
                        ? DateTime.Parse(parts[7])
                        : DateTime.Parse(parts[5]); // fallback для старих записів

                    var asset = new MediaAsset
                    {
                        Id = Guid.Parse(parts[0]),
                        FullPath = parts[1],
                        Comment = parts[3],
                        Status = Enum.Parse<FileStatus>(parts[4]),
                        Baseline = new AssetMetadata
                        {
                            RegisteredTime = DateTime.Parse(parts[5]),
                            RegisteredSize = long.Parse(parts[6]),
                            FirstSeenTime = firstSeen
                        }
                    };
                    dict[asset.FullPath] = asset;
                }
                catch { }
            }
            return dict;
        }

        public IEnumerable<IAsset> LoadAll() => _cache;

        public void Commit()
        {
            var lines = _cache.Select(a =>
                $"{a.Id};{a.FullPath};{a.Name};{a.Comment};{a.Status};" +
                $"{a.Baseline.RegisteredTime:O};{a.Baseline.RegisteredSize};" +
                $"{a.Baseline.FirstSeenTime:O}");
            File.WriteAllLines(_filePath, lines);
        }
    }
}
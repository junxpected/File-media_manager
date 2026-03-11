using File_manager.Interfaces;
using File_manager.Models;
using System.IO;
using System.Linq;

namespace File_manager.Services
{
    public class SSVRepository : IRepository<IAsset>
    {
        private readonly string _filePath;

        // Всі файли з усіх папок — ключ: FullPath
        private readonly Dictionary<string, IAsset> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        private bool _csvLoaded = false;

        public SSVRepository(string filePath)
        {
            _filePath = filePath;
        }

        // Завантажує CSV в кеш один раз при старті
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

        // Повертає збережені дані для конкретної папки
        public Dictionary<string, IAsset> LoadAsDictionary()
        {
            EnsureLoaded();
            return _cache;
        }

        // Додає або оновлює запис — той самий об'єкт що і в Assets
        public void Save(IAsset item)
        {
            EnsureLoaded();
            _cache[item.FullPath] = item;
        }

        // Видаляє запис (при видаленні файлу)
        public void Remove(IAsset item)
        {
            _cache.Remove(item.FullPath);
        }

        public IEnumerable<IAsset> LoadAll() => _cache.Values;

        // Записує ВСІ папки в CSV — не тільки поточну
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
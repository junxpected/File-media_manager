using File_manager.Interfaces;
using System.IO;

namespace File_manager.Services
{
    public class FileSystemMonitor : IFileWatcher
    {
        private FileSystemWatcher? _watcher;

        public event Action<FileSystemEventArgs>? OnFileSystemChanged;

        public void Start(string path)
        {
            _watcher = new FileSystemWatcher(path);

            _watcher.NotifyFilter = NotifyFilters.LastWrite
                                  | NotifyFilters.Size
                                  | NotifyFilters.FileName
                                  | NotifyFilters.DirectoryName;

            _watcher.IncludeSubdirectories = true;
            _watcher.InternalBufferSize = 65536;
            // збільшуємо буфер для великих папок

            _watcher.Changed += (s, e) => OnFileSystemChanged?.Invoke(e);
            _watcher.Created += (s, e) => OnFileSystemChanged?.Invoke(e);
            _watcher.Deleted += (s, e) => OnFileSystemChanged?.Invoke(e);
            _watcher.Renamed += (s, e) => OnFileSystemChanged?.Invoke(e);
            _watcher.EnableRaisingEvents = true;
        }


        public void Stop()
        {
            _watcher?.Dispose();
        }
    }
}

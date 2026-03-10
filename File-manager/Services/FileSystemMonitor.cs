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

            _watcher.Changed += (s, e) => OnFileSystemChanged?.Invoke(e);
            _watcher.Created += (s, e) => OnFileSystemChanged?.Invoke(e);
            _watcher.Deleted += (s, e) => OnFileSystemChanged?.Invoke(e);
            _watcher.EnableRaisingEvents = true;
        }
       

        public void Stop() 
        {
            _watcher?.Dispose();
        }
    }
}

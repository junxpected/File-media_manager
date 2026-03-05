using System.IO;

namespace File_manager.Interfaces
{
    public interface IFileWatcher
    {
        event Action <FileSystemEventArgs> OnFileSystemChanged;
        void Start(string path);
        void Stop();
    }
}

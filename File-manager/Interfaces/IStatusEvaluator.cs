using File_manager.Models;
using System.IO;

namespace File_manager.Interfaces
{
    public interface IStatusEvaluator
    {
        /// <summary>Розраховує стан для нового файлу (без збереженого стану)</summary>
        FileStatus CalculateStatus(FileSystemInfo current, AssetMetadata baseline);

        /// <summary>Розраховує стан з урахуванням поточного ручного стану</summary>
        FileStatus ResolveStatus(FileSystemInfo current, AssetMetadata baseline,
                                  FileStatus currentStatus);
    }
}
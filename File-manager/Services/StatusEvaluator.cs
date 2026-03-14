using File_manager.Interfaces;
using File_manager.Models;
using System.IO;

namespace File_manager.Services
{
    public class StatusEvaluator : IStatusEvaluator
    {

        public FileStatus CalculateStatus(FileSystemInfo current, AssetMetadata baseline)
        {
            return ResolveStatus(current, baseline, FileStatus.New);
        }

        public FileStatus ResolveStatus(FileSystemInfo current, AssetMetadata baseline,
                                 FileStatus savedStatus)
        {
            if (!current.Exists)
                return FileStatus.Missing;

            if (current is FileInfo file)
            {
                bool changed = Math.Abs((file.LastWriteTime - baseline.RegisteredTime).TotalSeconds) > 2 || file.Length != baseline.RegisteredSize;

            if (changed)
                return FileStatus.Modified;
            }

            if (savedStatus == FileStatus.Approved || savedStatus == FileStatus.Rejected || savedStatus == FileStatus.Done)
                return savedStatus;
                return FileStatus.New;
        }
    }
}
using File_manager.Interfaces;
using File_manager.Models;
using System.IO;

namespace File_manager.Services
{
    public class StatusEvaluator : IStatusEvaluator
    {
        private const int NewThresholdDays = 3;

        public FileStatus CalculateStatus(FileSystemInfo current, AssetMetadata baseline)
        {
            return ResolveStatus(current, baseline, FileStatus.Locked);
        }

        public FileStatus ResolveStatus(FileSystemInfo current, AssetMetadata baseline, FileStatus savedStatus)
        {
            if (!current.Exists)
                return FileStatus.Missing;

            if (current is FileInfo file)
            {
                bool changed = Math.Abs((file.LastWriteTime - baseline.RegisteredTime).TotalSeconds) > 2
                            || file.Length != baseline.RegisteredSize;

                if (changed)
                    return FileStatus.Modified;
            }

            if (savedStatus == FileStatus.Approved ||
                savedStatus == FileStatus.Rejected ||
                savedStatus == FileStatus.Done)
                return savedStatus;

            if ((DateTime.Now - baseline.FirstSeenTime).TotalDays <= NewThresholdDays)
                return FileStatus.New;

            return FileStatus.New;
        }
    }
}
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
            if (!current.Exists)
                return FileStatus.Missing;

            if (current is FileInfo fileInfo)
            {
                // Файл змінився якщо час запису або розмір відрізняється від baseline
                bool isModified = fileInfo.LastWriteTime != baseline.RegisteredTime
                               || fileInfo.Length != baseline.RegisteredSize;

                if (isModified)
                    return FileStatus.Modified;
            }

            // Файл не змінений — перевіряємо вік по FirstSeenTime
            var age = DateTime.Now - baseline.FirstSeenTime;
            if (age.TotalDays <= NewThresholdDays)
                return FileStatus.New;

            return FileStatus.Locked; // "без статусу" у відображенні
        }
    }
}
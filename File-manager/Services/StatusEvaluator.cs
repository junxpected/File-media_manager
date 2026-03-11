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

        public FileStatus ResolveStatus(FileSystemInfo current, AssetMetadata baseline,
                                         FileStatus savedStatus)
        {
            // 1. Файл зник
            if (!current.Exists)
                return FileStatus.Missing;

            if (current is FileInfo file)
            {
                bool changed = Math.Abs((file.LastWriteTime - baseline.RegisteredTime).TotalSeconds) > 2
                            || file.Length != baseline.RegisteredSize;

                // 2. Файл змінився — Modified завжди, навіть якщо New чи Approved
                if (changed)
                    return FileStatus.Modified;
            }

            // 3. Файл не змінився — зберігаємо ручний стан
            if (savedStatus == FileStatus.Approved ||
                savedStatus == FileStatus.Rejected ||
                savedStatus == FileStatus.Done)
                return savedStatus;

            // 4. New — файл молодший 3 днів (по CreationTime, не по RegisetredTime)
            if ((DateTime.Now - baseline.FirstSeenTime).TotalDays <= NewThresholdDays)
                return FileStatus.New;

            // 5. Старий, без змін — порожньо
            return FileStatus.Locked;
        }
    }
}
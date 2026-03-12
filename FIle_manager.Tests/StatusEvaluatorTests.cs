using File_manager.Models;
using File_manager.Services;
using System;
using System.IO;
using Xunit;

namespace File_manager.Tests
{
    public class StatusEvaluatorTests
    {
        private readonly StatusEvaluator _sut = new();

        // ── Helpers ─────────────────────────────────────────────────
        private static AssetMetadata Baseline(
            DateTime? registeredTime = null,
            long size = 100,
            DateTime? firstSeen = null) => new()
        {
            RegisteredTime = registeredTime ?? DateTime.Now.AddSeconds(-10),
            RegisteredSize = size,
            FirstSeenTime  = firstSeen  ?? DateTime.Now
        };

        // ── CalculateStatus ──────────────────────────────────────────

        [Fact]
        public void CalculateStatus_FileNotExists_ReturnsMissing()
        {
            var fake = new FileInfo("C:\\does_not_exist_xyz.txt");
            var result = _sut.CalculateStatus(fake, Baseline());
            Assert.Equal(FileStatus.Missing, result);
        }

        [Fact]
        public void CalculateStatus_NewFile_ReturnsNew()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(fi.LastWriteTime, fi.Length, DateTime.Now);
                var result   = _sut.CalculateStatus(fi, baseline);
                Assert.Equal(FileStatus.New, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void CalculateStatus_OldUnchangedFile_ReturnsLocked()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(fi.LastWriteTime, fi.Length,
                    firstSeen: DateTime.Now.AddDays(-10));
                var result   = _sut.CalculateStatus(fi, baseline);
                Assert.Equal(FileStatus.Locked, result);
            }
            finally { File.Delete(tmp); }
        }

        // ── ResolveStatus ────────────────────────────────────────────

        [Fact]
        public void ResolveStatus_FileMissing_ReturnsMissing()
        {
            var fake   = new FileInfo("C:\\no_such_file_abc.txt");
            var result = _sut.ResolveStatus(fake, Baseline(), FileStatus.Approved);
            Assert.Equal(FileStatus.Missing, result);
        }

        [Fact]
        public void ResolveStatus_SizeChanged_ReturnsModified()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "hello world");
                var fi       = new FileInfo(tmp);
                // baseline має інший розмір
                var baseline = Baseline(fi.LastWriteTime, size: 1);
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.Approved);
                Assert.Equal(FileStatus.Modified, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void ResolveStatus_TimeChanged_ReturnsModified()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var fi       = new FileInfo(tmp);
                // baseline має старіший час
                var baseline = Baseline(DateTime.Now.AddHours(-2), fi.Length);
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.New);
                Assert.Equal(FileStatus.Modified, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void ResolveStatus_UnchangedApproved_ReturnsApproved()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(fi.LastWriteTime, fi.Length,
                    firstSeen: DateTime.Now.AddDays(-10));
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.Approved);
                Assert.Equal(FileStatus.Approved, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void ResolveStatus_UnchangedRejected_ReturnsRejected()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(fi.LastWriteTime, fi.Length,
                    firstSeen: DateTime.Now.AddDays(-10));
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.Rejected);
                Assert.Equal(FileStatus.Rejected, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void ResolveStatus_UnchangedDone_ReturnsDone()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(fi.LastWriteTime, fi.Length,
                    firstSeen: DateTime.Now.AddDays(-10));
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.Done);
                Assert.Equal(FileStatus.Done, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void ResolveStatus_YoungUnchangedFile_ReturnsNew()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(fi.LastWriteTime, fi.Length,
                    firstSeen: DateTime.Now);
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.Locked);
                Assert.Equal(FileStatus.New, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void ResolveStatus_ModifiedOverridesApproved()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "changed content");
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(fi.LastWriteTime, size: 1);
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.Approved);
                // Modified має вищий пріоритет навіть над Approved
                Assert.Equal(FileStatus.Modified, result);
            }
            finally { File.Delete(tmp); }
        }
    }
}
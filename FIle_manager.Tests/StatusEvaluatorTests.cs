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

        private static AssetMetadata Baseline(
            long size = 100,
            DateTime? registeredTime = null,
            DateTime? firstSeen = null) => new()
        {
            RegisteredTime = registeredTime ?? DateTime.Now.AddSeconds(-10),
            RegisteredSize = size,
            FirstSeenTime  = firstSeen ?? DateTime.Now
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
        public void CalculateStatus_UnchangedFile_ReturnsNew()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(fi.Length, registeredTime: fi.LastWriteTime);
                var result   = _sut.CalculateStatus(fi, baseline);
                Assert.Equal(FileStatus.New, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void CalculateStatus_SizeChanged_ReturnsModified()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "some content");
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(size: 1, registeredTime: fi.LastWriteTime);
                var result   = _sut.CalculateStatus(fi, baseline);
                Assert.Equal(FileStatus.Modified, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void CalculateStatus_ZeroSizeFile_ReturnsNew()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(size: 0, registeredTime: fi.LastWriteTime);
                var result   = _sut.CalculateStatus(fi, baseline);
                Assert.Equal(FileStatus.New, result);
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
                var baseline = Baseline(size: 1, registeredTime: fi.LastWriteTime);
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.Approved);
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
                var baseline = Baseline(fi.Length, registeredTime: fi.LastWriteTime);
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
                var baseline = Baseline(fi.Length, registeredTime: fi.LastWriteTime);
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
                var baseline = Baseline(fi.Length, registeredTime: fi.LastWriteTime);
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.Done);
                Assert.Equal(FileStatus.Done, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void ResolveStatus_UnchangedFile_ReturnsNew()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(fi.Length, registeredTime: fi.LastWriteTime);
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.New);
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
                var baseline = Baseline(size: 1, registeredTime: fi.LastWriteTime);
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.Approved);
                Assert.Equal(FileStatus.Modified, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void ResolveStatus_SizeChanged_OverridesRejected()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "changed content");
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(size: 1, registeredTime: fi.LastWriteTime);
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.Rejected);
                Assert.Equal(FileStatus.Modified, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void ResolveStatus_SizeChanged_OverridesDone()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "changed content");
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(size: 1, registeredTime: fi.LastWriteTime);
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.Done);
                Assert.Equal(FileStatus.Modified, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void ResolveStatus_SizeChanged_OverridesNew()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tmp, "changed content");
                var fi       = new FileInfo(tmp);
                var baseline = Baseline(size: 1, registeredTime: fi.LastWriteTime);
                var result   = _sut.ResolveStatus(fi, baseline, FileStatus.New);
                Assert.Equal(FileStatus.Modified, result);
            }
            finally { File.Delete(tmp); }
        }

        [Fact]
        public void ResolveStatus_MissingFile_OverridesApproved()
        {
            var fake   = new FileInfo("C:\\no_such_file_abc.txt");
            var result = _sut.ResolveStatus(fake, Baseline(), FileStatus.Done);
            Assert.Equal(FileStatus.Missing, result);
        }
    }
}
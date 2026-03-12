using File_manager.Interfaces;
using System.IO;

namespace File_manager.Services
{
    // Конкретні реалізації правил для кожного типу проекту
    public class UnityIgnoreRule : IProjectIgnoreRule
    {
        private static readonly HashSet<string> _dirs = new(StringComparer.OrdinalIgnoreCase)
            { "Library", "Temp", "Logs", "Build", "Builds", "UserSettings" };

        public bool ShouldIgnoreDirectory(string dirName) => _dirs.Contains(dirName);
    }

    public class UnrealIgnoreRule : IProjectIgnoreRule
    {
        private static readonly HashSet<string> _dirs = new(StringComparer.OrdinalIgnoreCase)
            { "Binaries", "Build", "Intermediate", "Saved", "DerivedDataCache" };

        public bool ShouldIgnoreDirectory(string dirName) => _dirs.Contains(dirName);
    }

    public class NodeIgnoreRule : IProjectIgnoreRule
    {
        public bool ShouldIgnoreDirectory(string dirName) =>
            dirName.Equals("node_modules", StringComparison.OrdinalIgnoreCase);
    }

    public class DotNetIgnoreRule : IProjectIgnoreRule
    {
        public bool ShouldIgnoreDirectory(string dirName) =>
            dirName.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
            dirName.Equals("obj", StringComparison.OrdinalIgnoreCase);
    }

    public class PythonIgnoreRule : IProjectIgnoreRule
    {
        public bool ShouldIgnoreDirectory(string dirName) =>
            dirName.Equals("__pycache__", StringComparison.OrdinalIgnoreCase) ||
            dirName.Equals(".mypy_cache", StringComparison.OrdinalIgnoreCase);
    }

    public static class IgnoreRules
    {
        private static readonly HashSet<string> _alwaysIgnoreDirs = new(StringComparer.OrdinalIgnoreCase)
            { ".git", ".svn", ".hg", ".vs", ".idea", ".vscode", ".gradle", "Pods" };

        private static readonly HashSet<string> _alwaysIgnoreExts = new(StringComparer.OrdinalIgnoreCase)
            { ".meta", ".tmp", ".temp", ".lock", ".suo", ".user", ".cache" };

        // Маппінг типу проекту → правила (Open/Closed: додати новий тип = новий клас + рядок тут)
        private static readonly Dictionary<ProjectType, IProjectIgnoreRule> _rules = new()
        {
            { ProjectType.Unity,  new UnityIgnoreRule()  },
            { ProjectType.Unreal, new UnrealIgnoreRule() },
            { ProjectType.Node,   new NodeIgnoreRule()   },
            { ProjectType.DotNet, new DotNetIgnoreRule() },
            { ProjectType.Python, new PythonIgnoreRule() },
        };

        public static ProjectType DetectProjectType(string rootPath)
        {
            if (File.Exists(Path.Combine(rootPath, "ProjectSettings", "ProjectVersion.txt")) ||
                (Directory.Exists(Path.Combine(rootPath, "Assets")) &&
                 Directory.Exists(Path.Combine(rootPath, "Library"))))
                return ProjectType.Unity;

            if (Directory.GetFiles(rootPath, "*.uproject", SearchOption.TopDirectoryOnly).Any())
                return ProjectType.Unreal;

            if (File.Exists(Path.Combine(rootPath, "package.json")))
                return ProjectType.Node;

            if (Directory.GetFiles(rootPath, "*.csproj", SearchOption.TopDirectoryOnly).Any() ||
                Directory.GetFiles(rootPath, "*.sln", SearchOption.TopDirectoryOnly).Any())
                return ProjectType.DotNet;

            if (File.Exists(Path.Combine(rootPath, "requirements.txt")) ||
                File.Exists(Path.Combine(rootPath, "setup.py")))
                return ProjectType.Python;

            return ProjectType.Generic;
        }

        public static bool ShouldIgnoreDirectory(string dirName, ProjectType projectType)
        {
            if (dirName.StartsWith(".")) return true;
            if (_alwaysIgnoreDirs.Contains(dirName)) return true;

            return _rules.TryGetValue(projectType, out var rule) &&
                   rule.ShouldIgnoreDirectory(dirName);
        }

        public static bool ShouldIgnoreFile(string fileName)
        {
            if (fileName.StartsWith(".")) return true;
            return _alwaysIgnoreExts.Contains(Path.GetExtension(fileName));
        }

        public static IEnumerable<string> GetFiles(string rootPath, ProjectType projectType,
                                                    CancellationToken ct = default)
        {
            var queue = new Queue<string>();
            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var dir = queue.Dequeue();

                string[] files;
                try { files = Directory.GetFiles(dir); }
                catch { continue; }

                foreach (var f in files)
                    if (!ShouldIgnoreFile(Path.GetFileName(f)))
                        yield return f;

                string[] subdirs;
                try { subdirs = Directory.GetDirectories(dir); }
                catch { continue; }

                foreach (var sub in subdirs)
                {
                    var name = Path.GetFileName(sub);
                    if (!ShouldIgnoreDirectory(name, projectType))
                        queue.Enqueue(sub);
                }
            }
        }
    }

    public enum ProjectType { Generic, Unity, Unreal, Node, DotNet, Python }
}
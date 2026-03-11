using System.IO;

namespace File_manager.Services
{
    /// <summary>
    /// Визначає які папки/файли ігнорувати залежно від типу проекту.
    /// Підтримує: Unity, Unreal, Node.js, .NET, Python, Git-репо.
    /// </summary>
    public static class IgnoreRules
    {
        // Папки які ЗАВЖДИ ігноруємо незалежно від проекту
        private static readonly HashSet<string> AlwaysIgnoreDirs = new(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".svn", ".hg",           // VCS
            ".vs", ".idea", ".vscode",        // IDE
            "__pycache__", ".mypy_cache",     // Python
            "node_modules",                   // Node
            "bin", "obj",                     // .NET build
            ".gradle", ".kotlin",             // Android
            "Pods",                           // iOS
        };

        // Папки специфічні для Unity
        private static readonly HashSet<string> UnityIgnoreDirs = new(StringComparer.OrdinalIgnoreCase)
        {
            "Library", "Temp", "Logs", "obj",
            "Build", "Builds", "UserSettings"
        };

        // Папки для Unreal Engine
        private static readonly HashSet<string> UnrealIgnoreDirs = new(StringComparer.OrdinalIgnoreCase)
        {
            "Binaries", "Build", "Intermediate", "Saved", "DerivedDataCache"
        };

        // Розширення які завжди ігноруємо
        private static readonly HashSet<string> AlwaysIgnoreExts = new(StringComparer.OrdinalIgnoreCase)
        {
            ".meta",    // Unity meta files
            ".tmp", ".temp",
            ".lock",
            ".DS_Store",
            ".suo", ".user",
            ".cache",
        };

        public static ProjectType DetectProjectType(string rootPath)
        {
            if (File.Exists(Path.Combine(rootPath, "ProjectSettings", "ProjectVersion.txt")) ||
                Directory.Exists(Path.Combine(rootPath, "Assets")) &&
                Directory.Exists(Path.Combine(rootPath, "Library")))
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
            if (AlwaysIgnoreDirs.Contains(dirName)) return true;

            return projectType switch
            {
                ProjectType.Unity  => UnityIgnoreDirs.Contains(dirName),
                ProjectType.Unreal => UnrealIgnoreDirs.Contains(dirName),
                _ => false
            };
        }

        public static bool ShouldIgnoreFile(string fileName)
        {
            if (fileName.StartsWith(".")) return true;
            var ext = Path.GetExtension(fileName);
            return AlwaysIgnoreExts.Contains(ext);
        }

        /// <summary>Рекурсивно збирає файли з урахуванням правил ігнорування</summary>
        public static IEnumerable<string> GetFiles(string rootPath, ProjectType projectType,
                                                    CancellationToken ct = default)
        {
            var queue = new Queue<string>();
            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                ct.ThrowIfCancellationRequested();
                var dir = queue.Dequeue();

                // Файли в поточній папці
                string[] files;
                try { files = Directory.GetFiles(dir); }
                catch { continue; }

                foreach (var f in files)
                {
                    if (!ShouldIgnoreFile(Path.GetFileName(f)))
                        yield return f;
                }

                // Підпапки
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
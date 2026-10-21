using System;
using System.IO;

namespace StsCardEditor
{
    public static class AppPaths
    {
        public static string AppDir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar); }
        }

        public static string DataDir
        {
            get { return Path.Combine(AppDir, "data"); }
        }

        public static string BackupDir
        {
            get { return Path.Combine(DataDir, "backups"); }
        }

        public static string ToolsDir
        {
            get { return Path.Combine(AppDir, "tools"); }
        }

        public static string ProfilePath
        {
            get { return Path.Combine(DataDir, "profile.json"); }
        }

        public static string CatalogPath
        {
            get { return Path.Combine(AppDir, "catalog.json"); }
        }

        public static string AppLogPath
        {
            get { return Path.Combine(DataDir, "app.log"); }
        }

        public static string SettingsPath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "STSCardEditor");
                return Path.Combine(dir, "settings.json");
            }
        }

        public static string ContentPath(string gameDir)
        {
            return Path.Combine(Path.Combine(gameDir, "sts-card-editor"), "content.json");
        }

        public static string PatchJarPath(string gameDir)
        {
            return Path.Combine(Path.Combine(gameDir, "sts-card-editor"), "patch.jar");
        }

        public static string RuntimeLogPath(string gameDir)
        {
            return Path.Combine(Path.Combine(gameDir, "sts-card-editor"), "runtime.log");
        }

        public static void EnsureDataDirs()
        {
            if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
            if (!Directory.Exists(BackupDir)) Directory.CreateDirectory(BackupDir);
            string settingsDir = Path.GetDirectoryName(SettingsPath);
            if (!Directory.Exists(settingsDir)) Directory.CreateDirectory(settingsDir);
        }
    }

    public static class Logger
    {
        private static readonly object Sync = new object();

        public static void Log(string message)
        {
            try
            {
                AppPaths.EnsureDataDirs();
                lock (Sync)
                {
                    using (StreamWriter w = new StreamWriter(AppPaths.AppLogPath, true, System.Text.Encoding.UTF8))
                    {
                        w.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message);
                    }
                }
            }
            catch
            {
            }
        }

        public static string Tail(int maxChars)
        {
            try
            {
                if (!File.Exists(AppPaths.AppLogPath)) return "";
                string text = File.ReadAllText(AppPaths.AppLogPath, System.Text.Encoding.UTF8);
                if (text.Length <= maxChars) return text;
                return text.Substring(text.Length - maxChars);
            }
            catch (Exception e)
            {
                return "读取日志失败: " + e.Message;
            }
        }
    }
}

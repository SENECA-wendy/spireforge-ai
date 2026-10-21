using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace StsCardEditor
{
    public static class GameLocator
    {
        public static string FindGameDir()
        {
            List<string> libraries = new List<string>();
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (k != null)
                    {
                        string p = k.GetValue("SteamPath") as string;
                        if (!string.IsNullOrEmpty(p)) libraries.Add(p.Replace('/', '\\'));
                    }
                }
            }
            catch
            {
            }
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    if (k != null)
                    {
                        string p = k.GetValue("SteamExe") as string;
                        if (!string.IsNullOrEmpty(p))
                        {
                            string dir = Path.GetDirectoryName(p.Replace('/', '\\'));
                            if (!string.IsNullOrEmpty(dir) && !libraries.Contains(dir)) libraries.Add(dir);
                        }
                    }
                }
            }
            catch
            {
            }
            foreach (string s in new string[] {
                @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam",
                @"D:\steam", @"D:\Steam", @"E:\steam", @"E:\Steam", @"D:\SteamLibrary", @"E:\SteamLibrary" })
            {
                if (Directory.Exists(s) && !libraries.Contains(s)) libraries.Add(s);
            }
            foreach (string steam in libraries.ToArray())
            {
                string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (File.Exists(vdf))
                {
                    try
                    {
                        string text = File.ReadAllText(vdf, Encoding.UTF8);
                        foreach (Match m in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\""))
                        {
                            string p = m.Groups[1].Value.Replace("\\\\", "\\").Replace('/', '\\');
                            if (!libraries.Contains(p)) libraries.Add(p);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            foreach (string steam in libraries)
            {
                string game = Path.Combine(steam, "steamapps", "common", "SlayTheSpire");
                if (IsGameDir(game)) return game;
                game = Path.Combine(steam, "common", "SlayTheSpire");
                if (IsGameDir(game)) return game;
            }
            return null;
        }

        public static bool IsGameDir(string dir)
        {
            return !string.IsNullOrEmpty(dir)
                && File.Exists(Path.Combine(dir, "desktop-1.0.jar"))
                && File.Exists(Path.Combine(dir, "config.json"))
                && File.Exists(Path.Combine(dir, "SlayTheSpire.exe"));
        }

        public static string GetBuildId(string gameDir)
        {
            try
            {
                string common = Path.GetDirectoryName(gameDir.TrimEnd(Path.DirectorySeparatorChar));
                string steamapps = Path.GetDirectoryName(common);
                string acf = Path.Combine(steamapps, "appmanifest_646570.acf");
                if (File.Exists(acf))
                {
                    Match m = Regex.Match(File.ReadAllText(acf), "\"buildid\"\\s*\"(\\d+)\"");
                    if (m.Success) return m.Groups[1].Value;
                }
            }
            catch
            {
            }
            return "未知";
        }

        public static string HashFile(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hash = sha.ComputeHash(fs);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        public static bool IsGameRunning()
        {
            try
            {
                foreach (Process p in Process.GetProcesses())
                {
                    if (p.ProcessName == "SlayTheSpire") return true;
                    if (p.ProcessName == "javaw")
                    {
                        try
                        {
                            if (p.MainWindowTitle == "Slay the Spire") return true;
                        }
                        catch
                        {
                        }
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        public static string OriginalJarBackup(string gameDir)
        {
            return Path.Combine(gameDir, "desktop-1.0-original.jar");
        }

        public static bool IsWrapperJar(string gameDir)
        {
            try
            {
                string wrapper = Path.Combine(gameDir, "desktop-1.0.jar");
                string original = OriginalJarBackup(gameDir);
                if (!File.Exists(wrapper) || !File.Exists(original)) return false;
                if (new FileInfo(wrapper).Length > 2 * 1024 * 1024) return false;
                using (System.IO.Compression.ZipArchive zip = System.IO.Compression.ZipFile.OpenRead(wrapper))
                {
                    System.IO.Compression.ZipArchiveEntry e = zip.GetEntry("META-INF/MANIFEST.MF");
                    if (e == null) return false;
                    using (StreamReader r = new StreamReader(e.Open(), Encoding.UTF8))
                    {
                        string text = r.ReadToEnd();
                        return text.IndexOf("com.stseditor.bootstrap.Bootstrap", StringComparison.Ordinal) >= 0;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        public static string GameJarForHash(string gameDir)
        {
            if (IsWrapperJar(gameDir)) return OriginalJarBackup(gameDir);
            return Path.Combine(gameDir, "desktop-1.0.jar");
        }

        public static bool IsPatchInstalled(string gameDir)
        {
            return IsWrapperJar(gameDir) && File.Exists(AppPaths.PatchJarPath(gameDir));
        }

        public static GameInfo GetInfo(string gameDir, Catalog catalog)
        {
            GameInfo info = new GameInfo();
            info.directory = gameDir;
            info.configPath = Path.Combine(gameDir, "config.json");
            info.running = IsGameRunning();
            info.patchInstalled = IsPatchInstalled(gameDir);
            info.buildId = GetBuildId(gameDir);
            try
            {
                info.jarHash = HashFile(GameJarForHash(gameDir));
            }
            catch (Exception e)
            {
                info.jarHash = "读取失败: " + e.Message;
            }
            info.version = catalog == null ? "未知" : catalog.gameVersion;
            if (catalog != null && catalog.jarSha256 != null)
            {
                info.compatible = string.Equals(info.jarHash, catalog.jarSha256, StringComparison.OrdinalIgnoreCase);
                info.compatibilityText = info.compatible
                    ? "兼容：游戏文件哈希与卡牌目录一致"
                    : "不兼容：游戏文件已变化。为避免损坏，工具将拒绝写入补丁。请更新 catalog.json 或恢复对应版本。";
            }
            else
            {
                info.compatible = false;
                info.compatibilityText = "缺少 catalog.json，无法确认版本。";
            }
            return info;
        }
    }
}

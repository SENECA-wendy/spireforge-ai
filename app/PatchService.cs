using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace StsCardEditor
{
    public static class PatchService
    {
        public static string OriginalConfigPath
        {
            get { return Path.Combine(AppPaths.DataDir, "original-config.json"); }
        }

        public static void Apply(string gameDir, Catalog catalog, Profile profile, GameInfo info)
        {
            if (!GameLocator.IsGameDir(gameDir)) throw new InvalidOperationException("游戏目录无效。");
            if (GameLocator.IsGameRunning()) throw new InvalidOperationException("游戏正在运行。请先退出游戏，再应用或恢复补丁。");
            if (!info.compatible) throw new InvalidOperationException(info.compatibilityText);
            AppPaths.EnsureDataDirs();
            string configPath = Path.Combine(gameDir, "config.json");
            string currentJar = Path.Combine(gameDir, "desktop-1.0.jar");
            string originalJar = GameLocator.OriginalJarBackup(gameDir);
            bool wrapper = GameLocator.IsWrapperJar(gameDir);

            if (!File.Exists(OriginalConfigPath))
            {
                Dictionary<string, object> cur = JsonUtil.FromJson(File.ReadAllText(configPath, Encoding.UTF8)) as Dictionary<string, object>;
                bool legacyPatch = IsLegacyConfig(cur);
                if (!legacyPatch)
                {
                    File.Copy(configPath, OriginalConfigPath, true);
                }
                else
                {
                    WriteDefaultConfig(OriginalConfigPath);
                }
                Logger.Log("保存原始 config.json: " + OriginalConfigPath);
            }

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string backupDir = Path.Combine(AppPaths.BackupDir, "install-" + stamp);
            Directory.CreateDirectory(backupDir);
            if (File.Exists(configPath)) File.Copy(configPath, Path.Combine(backupDir, "config-before-apply.json"), true);

            if (!wrapper)
            {
                if (!File.Exists(currentJar)) throw new FileNotFoundException("找不到 desktop-1.0.jar。", currentJar);
                if (new FileInfo(currentJar).Length < 2 * 1024 * 1024)
                {
                    throw new InvalidOperationException("desktop-1.0.jar 不是原版游戏 jar，且没有可恢复的 desktop-1.0-original.jar。为避免损坏，已停止。");
                }
                string currentHash = GameLocator.HashFile(currentJar);
                if (catalog != null && !string.Equals(currentHash, catalog.jarSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("desktop-1.0.jar 哈希与 catalog.json 不一致，已停止写入。");
                }
                if (File.Exists(originalJar))
                {
                    try { File.Delete(originalJar); } catch (Exception e) { throw new InvalidOperationException("无法替换旧的 desktop-1.0-original.jar: " + e.Message); }
                }
                File.Move(currentJar, originalJar);
                Logger.Log("重命名原版 jar: " + originalJar);
            }
            else
            {
                Logger.Log("检测到已安装的 wrapper，直接更新补丁。");
            }

            // Write declarative content. Preserve definitions that were already
            // installed in the game but are missing from the editor profile;
            // otherwise save entries using those IDs fall back to Madness.
            string contentPath = AppPaths.ContentPath(gameDir);
            if (ProfileStore.ImportGameContent(gameDir, profile))
            {
                Logger.Log("应用前已把游戏 content.json 中缺失的定义合并回编辑器配置。");
            }
            ContentDoc existingContent = ProfileStore.ReadContent(contentPath);
            ContentDoc doc = ProfileStore.BuildContent(profile);
            ProfileStore.MergeMissing(doc, existingContent);
            string contentJson = JsonUtil.ToJson(doc);
            string contentDir = Path.GetDirectoryName(contentPath);
            if (!Directory.Exists(contentDir)) Directory.CreateDirectory(contentDir);
            JsonUtil.WriteUtf8(contentPath, contentJson);
            Logger.Log("写入 content.json: " + contentPath + " bytes=" + Encoding.UTF8.GetByteCount(contentJson)
                + " custom=" + doc.customCards.Count + " overrides=" + doc.overrides.Count);

            // Build the class-shadowing jar from the user's own game jar.
            string patchJar = AppPaths.PatchJarPath(gameDir);
            BuildPatchJar(gameDir, patchJar);

            // Install the tiny wrapper jar as desktop-1.0.jar. Its manifest is:
            // Class-Path: sts-card-editor/patch.jar desktop-1.0-original.jar
            string wrapperTemplate = Path.Combine(AppPaths.ToolsDir, "wrapper.jar");
            if (!File.Exists(wrapperTemplate)) throw new FileNotFoundException("缺少 wrapper.jar。", wrapperTemplate);
            File.Copy(wrapperTemplate, currentJar, true);
            Logger.Log("安装 wrapper jar: " + currentJar);

            // The wrapper does the classpath work; config.json stays original.
            if (File.Exists(OriginalConfigPath)) File.Copy(OriginalConfigPath, configPath, true);
            else WriteDefaultConfig(configPath);

            Dictionary<string, object> install = new Dictionary<string, object>();
            install["installedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            install["gameDir"] = gameDir;
            install["gameBuild"] = info.buildId;
            install["gameJarSha256"] = GameLocator.HashFile(originalJar);
            install["patchJarSha256"] = GameLocator.HashFile(patchJar);
            install["wrapperSha256"] = GameLocator.HashFile(currentJar);
            install["originalConfigSha256"] = GameLocator.HashFile(OriginalConfigPath);
            install["customCards"] = doc.customCards.Count;
            install["overrides"] = doc.overrides.Count;
            install["method"] = "wrapper-jar + class-shadow patch";
            JsonUtil.WriteUtf8(Path.Combine(contentDir, "install.json"), JsonUtil.ToJson(install));
            Logger.Log("补丁应用完成: " + gameDir + " patch=" + install["patchJarSha256"] + " method=wrapper");
        }

        private static bool IsLegacyConfig(Dictionary<string, object> cfg)
        {
            if (cfg == null) return false;
            object cp = cfg.ContainsKey("classPath") ? cfg["classPath"] : null;
            ArrayList list = cp as ArrayList;
            if (list == null) return false;
            foreach (object o in list)
            {
                if (Convert.ToString(o).Replace('\\', '/') == "sts-card-editor/patch.jar") return true;
            }
            return false;
        }

        private static void WriteDefaultConfig(string path)
        {
            Dictionary<string, object> cfg = new Dictionary<string, object>();
            ArrayList cp = new ArrayList();
            cp.Add("desktop-1.0.jar");
            cfg["classPath"] = cp;
            cfg["mainClass"] = "com.megacrit.cardcrawl.desktop.DesktopLauncher";
            ArrayList vm = new ArrayList();
            vm.Add("-Xmx1G");
            vm.Add("-Dsun.java2d.dpiaware=true");
            cfg["vmArgs"] = vm;
            JsonUtil.WriteUtf8(path, JsonUtil.ToJson(cfg));
        }

        private static void BuildPatchJar(string gameDir, string patchJar)
        {
            string java = Path.Combine(gameDir, Path.Combine("jre", Path.Combine("bin", "java.exe")));
            if (!File.Exists(java)) throw new FileNotFoundException("找不到游戏自带 JRE，无法构建补丁。", java);
            string patcher = Path.Combine(AppPaths.ToolsDir, "patcher.jar");
            string asm = Path.Combine(AppPaths.ToolsDir, "asm-9.7.1.jar");
            string runtime = Path.Combine(AppPaths.ToolsDir, "runtime-classes");
            if (!File.Exists(patcher)) throw new FileNotFoundException("缺少 patcher.jar。", patcher);
            if (!File.Exists(asm)) throw new FileNotFoundException("缺少 asm-9.7.1.jar。", asm);
            if (!Directory.Exists(runtime)) throw new DirectoryNotFoundException("缺少 runtime-classes 目录: " + runtime);
            string desktop = GameLocator.OriginalJarBackup(gameDir);
            if (!File.Exists(desktop)) desktop = Path.Combine(gameDir, "desktop-1.0.jar");
            string args = "-cp \"" + patcher + ";" + asm + "\" com.stseditor.patcher.Patcher \""
                + desktop + "\" \"" + runtime + "\" \"" + patchJar + "\"";
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = java;
            psi.Arguments = args;
            psi.WorkingDirectory = AppPaths.AppDir;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            Logger.Log("构建补丁: " + java + " " + args);
            using (Process p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                Logger.Log("patcher stdout: " + stdout.Trim());
                if (!string.IsNullOrEmpty(stderr)) Logger.Log("patcher stderr: " + stderr.Trim());
                if (p.ExitCode != 0) throw new InvalidOperationException("构建补丁失败，退出码 " + p.ExitCode + "。" + stderr);
            }
            if (!File.Exists(patchJar) || new FileInfo(patchJar).Length < 1000)
            {
                throw new InvalidOperationException("补丁 jar 未生成或大小异常。");
            }
        }

        public static string Restore(string gameDir)
        {
            if (!GameLocator.IsGameDir(gameDir)) throw new InvalidOperationException("游戏目录无效。");
            if (GameLocator.IsGameRunning()) throw new InvalidOperationException("游戏正在运行。请先退出游戏。");
            string configPath = Path.Combine(gameDir, "config.json");
            string currentJar = Path.Combine(gameDir, "desktop-1.0.jar");
            string originalJar = GameLocator.OriginalJarBackup(gameDir);
            string source = File.Exists(OriginalConfigPath) ? OriginalConfigPath : null;
            if (!File.Exists(originalJar) && File.Exists(currentJar) && new FileInfo(currentJar).Length < 2 * 1024 * 1024)
            {
                throw new InvalidOperationException("当前 desktop-1.0.jar 是包装器，但找不到 desktop-1.0-original.jar，无法安全恢复。请先备份存档，并从 Steam 校验游戏文件或手动恢复原版 jar。");
            }
            if (source != null) File.Copy(source, configPath, true);

            if (File.Exists(originalJar))
            {
                if (!File.Exists(currentJar))
                {
                    File.Move(originalJar, currentJar);
                    Logger.Log("恢复原版 jar: " + originalJar + " -> " + currentJar);
                }
                else if (new FileInfo(currentJar).Length < 2 * 1024 * 1024)
                {
                    File.Delete(currentJar);
                    File.Move(originalJar, currentJar);
                    Logger.Log("恢复原版 jar: " + originalJar + " -> " + currentJar);
                }
                else
                {
                    // The current desktop-1.0.jar is already a full-size game jar
                    // (for example after a Steam verify/update). Never downgrade it.
                    Logger.Log("当前 desktop-1.0.jar 已是完整文件，保留当前文件，不覆盖；旧备份仍为 " + originalJar);
                }
            }
            string patch = AppPaths.PatchJarPath(gameDir);
            if (File.Exists(patch))
            {
                try { File.Delete(patch); } catch (Exception e) { Logger.Log("删除 patch.jar 失败: " + e.Message); }
            }
            Logger.Log("恢复原版启动完成: " + gameDir);
            return source == null ? "(未找到 config 备份，已尽量恢复 jar)" : source;
        }

        public static string GetInstallInfo(string gameDir)
        {
            try
            {
                string p = Path.Combine(Path.Combine(gameDir, "sts-card-editor"), "install.json");
                if (!File.Exists(p)) return null;
                return JsonUtil.ReadUtf8(p);
            }
            catch
            {
                return null;
            }
        }
    }
}

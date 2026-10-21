using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StsCardEditor
{
    public static class SaveEditor
    {
        private const string Key = "key";

        public static List<string> FindSaves(string gameDir)
        {
            List<string> list = new List<string>();
            string dir = Path.Combine(gameDir, "saves");
            if (!Directory.Exists(dir)) return list;
            foreach (string f in Directory.GetFiles(dir, "*.autosave")) list.Add(f);
            list.Sort();
            return list;
        }

        public static string Decode(string path)
        {
            byte[] raw = Convert.FromBase64String(File.ReadAllText(path, Encoding.ASCII).Trim());
            byte[] key = Encoding.UTF8.GetBytes(Key);
            byte[] plain = new byte[raw.Length];
            for (int i = 0; i < raw.Length; i++) plain[i] = (byte)(raw[i] ^ key[i % key.Length]);
            return Encoding.UTF8.GetString(plain);
        }

        public static void Encode(string path, string json)
        {
            byte[] plain = Encoding.UTF8.GetBytes(json);
            byte[] key = Encoding.UTF8.GetBytes(Key);
            byte[] raw = new byte[plain.Length];
            for (int i = 0; i < plain.Length; i++) raw[i] = (byte)(plain[i] ^ key[i % key.Length]);
            File.WriteAllText(path, Convert.ToBase64String(raw), Encoding.ASCII);
        }

        public static Dictionary<string, object> Load(string path)
        {
            return JsonUtil.FromJson(Decode(path)) as Dictionary<string, object>;
        }

        public static List<string> ReadDeck(string path)
        {
            List<string> ids = new List<string>();
            Dictionary<string, object> data = Load(path);
            if (data != null && data.ContainsKey("cards"))
            {
                IList cards = data["cards"] as IList;
                if (cards != null)
                {
                    foreach (object o in cards)
                    {
                        Dictionary<string, object> c = o as Dictionary<string, object>;
                        if (c != null && c.ContainsKey("id")) ids.Add(Convert.ToString(c["id"]));
                    }
                }
            }
            return ids;
        }

        public static string InjectCards(string gameDir, string savePath, string cardId, int count, bool upgraded)
        {
            if (GameLocator.IsGameRunning()) throw new InvalidOperationException("游戏正在运行。请先退出游戏，再修改存档。");
            if (!File.Exists(savePath)) throw new FileNotFoundException("找不到存档文件。", savePath);
            AppPaths.EnsureDataDirs();
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string backupDir = Path.Combine(AppPaths.BackupDir, "save-" + stamp);
            Directory.CreateDirectory(backupDir);
            string backupPath = Path.Combine(backupDir, Path.GetFileName(savePath));
            File.Copy(savePath, backupPath, true);
            Logger.Log("备份存档: " + backupPath);

            Dictionary<string, object> data = Load(savePath);
            if (data == null) throw new InvalidDataException("存档解码失败。");
            IList existing = data.ContainsKey("cards") ? data["cards"] as IList : null;
            ArrayList cards = new ArrayList();
            if (existing != null)
            {
                foreach (object o in existing) cards.Add(o);
            }
            data["cards"] = cards;
            for (int i = 0; i < count; i++)
            {
                Dictionary<string, object> c = new Dictionary<string, object>();
                c["upgrades"] = upgraded ? 1 : 0;
                c["misc"] = 0;
                c["id"] = cardId;
                cards.Add(c);
            }
            string json = JsonUtil.ToJson(data);
            Encode(savePath, json);
            Logger.Log("写入存档 " + savePath + "，加入 " + count + " 张 " + cardId + "，备份=" + backupPath);
            return backupPath;
        }

        public static void Restore(string backupPath, string savePath)
        {
            if (GameLocator.IsGameRunning()) throw new InvalidOperationException("游戏正在运行。请先退出游戏。");
            if (!File.Exists(backupPath)) throw new FileNotFoundException("找不到备份。", backupPath);
            File.Copy(backupPath, savePath, true);
            Logger.Log("恢复存档: " + backupPath + " -> " + savePath);
        }

        public static List<string> ListBackups()
        {
            List<string> list = new List<string>();
            if (!Directory.Exists(AppPaths.BackupDir)) return list;
            foreach (string d in Directory.GetDirectories(AppPaths.BackupDir))
            {
                if (Path.GetFileName(d).StartsWith("save-"))
                {
                    foreach (string f in Directory.GetFiles(d, "*.autosave")) list.Add(f);
                }
            }
            list.Sort();
            return list;
        }
    }
}

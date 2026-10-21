using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace StsCardEditor
{
    public class DeckEntry
    {
        public string cardId { get; set; }
        public string name { get; set; }
        public int count { get; set; }
        public int upgrades { get; set; }
        public int misc { get; set; }
        public DeckEntry() { count = 1; }
    }

    public class DeckPreset
    {
        public string id { get; set; }
        public string name { get; set; }
        public List<DeckEntry> entries { get; set; }
        public List<CustomCardModel> customCards { get; set; }
        public DeckPreset() { id = Guid.NewGuid().ToString("N"); entries = new List<DeckEntry>(); customCards = new List<CustomCardModel>(); }
        public override string ToString() { return name; }
    }

    public class DeckLibrary
    {
        public int schemaVersion { get; set; }
        public List<DeckPreset> decks { get; set; }
        public DeckLibrary() { schemaVersion = 1; decks = new List<DeckPreset>(); }
    }

    public static class DeckStore
    {
        public static string PathName { get { return Path.Combine(AppPaths.DataDir, "decks.json"); } }

        public static DeckLibrary Load(string path)
        {
            if (!File.Exists(path)) return new DeckLibrary();
            DeckLibrary library = JsonUtil.FromJson<DeckLibrary>(JsonUtil.ReadUtf8(path));
            if (library == null || library.schemaVersion != 1 || library.decks == null)
                throw new InvalidDataException("卡组库格式无效，已保留原文件：" + path);
            HashSet<string> ids = new HashSet<string>();
            foreach (DeckPreset deck in library.decks)
            {
                if (deck == null || string.IsNullOrEmpty(deck.id) || !ids.Add(deck.id)) throw new InvalidDataException("卡组 ID 缺失或重复。");
                Validate(deck, false);
                if (deck.customCards == null) deck.customCards = new List<CustomCardModel>();
            }
            return library;
        }

        public static void Validate(DeckPreset deck, bool requireCards)
        {
            if (deck == null || string.IsNullOrWhiteSpace(deck.name) || deck.name.Length > 100) throw new InvalidDataException("卡组名称为 1 至 100 个字符。");
            if (deck.entries == null || deck.entries.Count > 500) throw new InvalidDataException("卡组条目无效。");
            int count = 0;
            foreach (DeckEntry entry in deck.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.cardId) || entry.count < 1 || entry.count > 100 || entry.upgrades < 0 || entry.upgrades > 100 || entry.misc < 0)
                    throw new InvalidDataException("卡牌 ID、数量（1~100）、升级次数（0~100）或附加数值无效。");
                count += entry.count;
            }
            if (count > 500 || (requireCards && count < 1)) throw new InvalidDataException("卡组需要 1 至 500 张牌。");
        }

        public static void Save(string path, DeckLibrary library)
        {
            foreach (DeckPreset deck in library.decks) Validate(deck, false);
            string dir = Path.GetDirectoryName(path);
            Directory.CreateDirectory(dir);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temp, JsonUtil.ToJson(library), new UTF8Encoding(false));
            try
            {
                if (File.Exists(path))
                {
                    string backups = Path.Combine(dir, "backups");
                    Directory.CreateDirectory(backups);
                    File.Replace(temp, path, Path.Combine(backups, "decks-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0,6) + ".json"));
                }
                else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}

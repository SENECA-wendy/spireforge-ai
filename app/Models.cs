using System;
using System.Collections.Generic;

namespace StsCardEditor
{
    public class Catalog
    {
        public int schemaVersion { get; set; }
        public string gameVersion { get; set; }
        public string jarSha256 { get; set; }
        public string source { get; set; }
        public List<CatalogCard> cards { get; set; }
    }

    public class CatalogCard
    {
        public string id { get; set; }
        public string className { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string upgradeDescription { get; set; }
        public int cost { get; set; }
        public string type { get; set; }
        public string color { get; set; }
        public string rarity { get; set; }
        public string target { get; set; }
        public int? damage { get; set; }
        public int? upgradeDamage { get; set; }
        public int? block { get; set; }
        public int? upgradeBlock { get; set; }
        public int? magicNumber { get; set; }
        public int? upgradeMagicNumber { get; set; }
        public int? upgradeCost { get; set; }
        public bool special { get; set; }
        public string specialNote { get; set; }
    }

    public class CardOverrideModel
    {
        public string name { get; set; }
        public string description { get; set; }
        public int? cost { get; set; }
        public int? upgradeCost { get; set; }
        public int? damage { get; set; }
        public int? upgradeDamage { get; set; }
        public int? block { get; set; }
        public int? upgradeBlock { get; set; }
        public int? magicNumber { get; set; }
        public int? upgradeMagicNumber { get; set; }
        public int? draw { get; set; }
        public int? discard { get; set; }
        public int? heal { get; set; }
    }

    public class Profile
    {
        public int schemaVersion { get; set; }
        public Dictionary<string, CardOverrideModel> overrides { get; set; }
        public List<CustomCardModel> customCards { get; set; }
        public List<string> deletedCustomCardIds { get; set; }

        public Profile()
        {
            schemaVersion = 2;
            overrides = new Dictionary<string, CardOverrideModel>();
            customCards = new List<CustomCardModel>();
            deletedCustomCardIds = new List<string>();
        }
    }

    public class CustomCardModel
    {
        public string id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string upgradeDescription { get; set; }
        public string image { get; set; }
        public string color { get; set; }
        public string type { get; set; }
        public string rarity { get; set; }
        public string target { get; set; }
        public int cost { get; set; }
        public int? upgradeCost { get; set; }
        public int? damage { get; set; }
        public int? upgradeDamage { get; set; }
        public int? block { get; set; }
        public int? upgradeBlock { get; set; }
        public int? magicNumber { get; set; }
        public int? upgradeMagicNumber { get; set; }
        public bool exhaust { get; set; }
        public bool innate { get; set; }
        public bool ethereal { get; set; }
        public bool retain { get; set; }
        public List<string> keywords { get; set; }
        public List<EffectModel> effects { get; set; }
        public bool enabled { get; set; }
        public string notes { get; set; }

        public CustomCardModel()
        {
            color = "RED";
            type = "ATTACK";
            rarity = "COMMON";
            target = "ENEMY";
            cost = 1;
            image = "red/attack/bash";
            keywords = new List<string>();
            effects = new List<EffectModel>();
            enabled = true;
        }
    }

    public class EffectModel
    {
        public string type { get; set; }
        public int? amount { get; set; }
        public int? upgradeAmount { get; set; }
        public int? hits { get; set; }
        public int? upgradeHits { get; set; }
        public string power { get; set; }
        public string target { get; set; }
        public string cardId { get; set; }
        public string destination { get; set; }
        public bool? cardUpgrade { get; set; }
        public string extensionId { get; set; }
        public Dictionary<string, object> parameters { get; set; }
        public ConditionModel condition { get; set; }
    }

    public class ConditionModel
    {
        public string type { get; set; }
        public string power { get; set; }
        public int? minAmount { get; set; }
        public int? percent { get; set; }
    }

    public class ContentDoc
    {
        public int schemaVersion { get; set; }
        public Dictionary<string, CardOverrideModel> overrides { get; set; }
        public List<CustomCardModel> customCards { get; set; }

        public ContentDoc()
        {
            schemaVersion = 2;
            overrides = new Dictionary<string, CardOverrideModel>();
            customCards = new List<CustomCardModel>();
        }
    }

    public class ApiSettings
    {
        public string baseUrl { get; set; }
        public string model { get; set; }
        public string apiKey { get; set; }
        public int timeoutSeconds { get; set; }

        public ApiSettings()
        {
            baseUrl = "";
            model = "";
            timeoutSeconds = 60;
        }
    }

    public class GameInfo
    {
        public string directory { get; set; }
        public string buildId { get; set; }
        public string jarHash { get; set; }
        public string version { get; set; }
        public bool compatible { get; set; }
        public string compatibilityText { get; set; }
        public bool running { get; set; }
        public bool patchInstalled { get; set; }
        public string configPath { get; set; }
    }
}

package com.megacrit.cardcrawl.localization;
import com.google.gson.*;
public class LocalizedStrings {
 public static JsonObject cards,keywords;
 public CardStrings getCardStrings(String id){if(cards==null||!cards.has(id))throw new IllegalArgumentException("missing card localization "+id);return new Gson().fromJson(cards.get(id),CardStrings.class);}
 public KeywordStrings getKeywordString(String id){if(keywords==null||!keywords.has(id))throw new IllegalArgumentException("missing keyword "+id);return new Gson().fromJson(keywords.get(id),KeywordStrings.class);}
}

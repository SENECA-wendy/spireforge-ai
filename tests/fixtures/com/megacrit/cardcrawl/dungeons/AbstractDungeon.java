package com.megacrit.cardcrawl.dungeons;
import com.megacrit.cardcrawl.characters.AbstractPlayer;
import com.megacrit.cardcrawl.rooms.AbstractRoom;
public class AbstractDungeon {
    public static AbstractPlayer player;
    public static int ascensionLevel, floorNum;
    public static com.megacrit.cardcrawl.random.Random cardRng = new com.megacrit.cardcrawl.random.Random();
    public static com.megacrit.cardcrawl.random.Random merchantRng = new com.megacrit.cardcrawl.random.Random();
    public static AbstractRoom room = new AbstractRoom();
    public static AbstractRoom getCurrRoom() { return room; }
}

package com.megacrit.cardcrawl.rooms;
public class AbstractRoom {
    public enum RoomPhase { COMBAT, COMPLETE }
    public RoomPhase phase = RoomPhase.COMBAT;
}

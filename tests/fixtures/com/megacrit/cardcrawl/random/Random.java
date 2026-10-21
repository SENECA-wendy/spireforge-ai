package com.megacrit.cardcrawl.random;
public class Random { private final java.util.Random value = new java.util.Random(1); public int random(int max) { return value.nextInt(max+1); } }

package com.stseditor.runtime;
public final class LiveCostPolicy {
    public static int afterBaseCostChange(int oldCost, int oldCostForTurn, int newCost) {
        if (newCost < 0 || oldCost < 0) return newCost;
        return Math.max(0, newCost + oldCostForTurn - oldCost);
    }
}

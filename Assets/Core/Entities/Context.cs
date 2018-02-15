﻿
public class Context {
    public enum ActionSource {
        Unknown,
        Loading,
        PlayerDrag,
        SituationStoreStacks,
        SituationEffect,
        SituationResults,
        GreedySlot,
        AnimEnd,
        Retire,
        Debug
    }

    public ActionSource actionSource;

    public Context(ActionSource actionSource) {
        this.actionSource = actionSource;
    }

    public bool IsManualAction() {
        return actionSource == ActionSource.PlayerDrag;
    }

}

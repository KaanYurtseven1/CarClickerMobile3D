using System;

[Serializable]
public class BoostModeControllerSaveData
{
    public bool isUnlocked;
    public int currentCharge;
    public BoostModeController.BoostState currentState;
    public float remainingTime;
    public long lastTimestamp;
}

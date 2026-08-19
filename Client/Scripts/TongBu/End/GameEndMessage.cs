using System;

[Serializable]
public class GameEndMessage
{
    public string type;                    // "GameEnd" ✓
    public string roomId;                  // ✓
    public long timestamp;                 // ✓
    public float remainingTime;            // ✓
    public int remainingRounds;            // ✓
    public string victoryCondition;        // ✓
    public int redTeamVictory;             // ✓
    public int blueTeamVictory;            // ✓
    public string winnerTeamId;            // ✓
    public string winnerTeamName;          // ✓
    public bool isSeriesEnd;               // ✓
}

using System.Collections.Generic;

[System.Serializable]
public class PlayerStateMessage
{
    public string type;              // "WorldState"
    public string roomId;
    public long timestamp;
    public List<GameStateTeam> teams; // ← 直接复用你现有的 GameStateTeam
}

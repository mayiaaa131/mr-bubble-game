using System;

/// <summary>
/// 游戏结果数据结构
/// </summary>
[Serializable]
public class GameResult
{
    public string gameId;              // 游戏ID
    public string winningTeam;         // 胜利队伍 ("red" 或 "blue")
    public int winningTeamScore;       // 胜利队伍得分
    public int losingTeamScore;        // 失败队伍得分
    public long endTime;               // 游戏结束时间戳
    public string reason;              // 游戏结束原因 ("time_up" / "team_eliminated" 等)

  
    public GameResult(string gameId, string winningTeam, int winningScore, int losingScore)
    {
        this.gameId = gameId;
        this.winningTeam = winningTeam;
        this.winningTeamScore = winningScore;
        this.losingTeamScore = losingScore;
        this.endTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        this.reason = "time_up";
    }


}

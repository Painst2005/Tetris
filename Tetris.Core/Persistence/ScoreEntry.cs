namespace Tetris.Persistence;

/// <summary>
/// 一条历史成绩记录，保存分数、等级、消行数、玩家名和时间戳。
/// </summary>
public sealed record ScoreEntry(
    int Id,
    int Score,
    int Lines,
    int Level,
    string PlayerName,
    DateTime AchievedAt)
{
    /// <summary>供单元测试和零 ID 新纪录构造使用。</summary>
    public ScoreEntry(int score, int lines, int level, string playerName, DateTime achievedAt)
        : this(0, score, lines, level, playerName, achievedAt)
    {
    }
}

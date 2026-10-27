namespace Tetris.Persistence;

/// <summary>排行榜查询的返回结果，包含状态码和数据，供界面区分「数据库损坏」与「无记录」。</summary>
public enum ScoreboardStatus
{
    /// <summary>查询成功，data 为完整结果。</summary>
    Ok,

    /// <summary>数据库文件损坏或版本不兼容；data 为空，error 含可读说明。</summary>
    Corrupted,

    /// <summary>数据库被占用或无权限，暂时不可读。</summary>
    AccessDenied,

    /// <summary>其他 I/O 异常（磁盘满、路径非法等）。</summary>
    IoError,

    /// <summary>成功连接但表中尚无任何成绩。</summary>
    Empty
}

/// <summary>
/// 排行榜仓储抽象：保存、查询历史成绩，并在数据库异常时返回可恢复的状态码。
/// 核心逻辑只依赖该接口，测试可用内存实现或损坏实现注入。
/// </summary>
public interface IScoreRepository
{
    /// <summary>保存一条成绩并返回自增 ID；数据库异常时抛出 <see cref="ScoreRepositoryException"/>。</summary>
    int Add(ScoreEntry entry);

    /// <summary>按分数降序返回前 topN 条成绩，包含空结果和损坏处理。</summary>
    ScoreboardResult GetTopScores(int topN = 10);

    /// <summary>返回所有历史成绩（按分数降序），不做数量限制。</summary>
    ScoreboardResult GetAllScores();

    /// <summary>清空所有历史成绩；数据库异常时抛出异常。</summary>
    void Clear();
}

/// <summary>
/// 排行榜查询结果包装。Status 非 Ok 时，Entries 为空集合，ErrorMessage 给出可读说明。
/// </summary>
public sealed class ScoreboardResult
{
    public ScoreboardResult(ScoreboardStatus status, IReadOnlyList<ScoreEntry> entries, string? errorMessage = null)
    {
        Status = status;
        Entries = entries;
        ErrorMessage = errorMessage;
    }

    public ScoreboardStatus Status { get; }

    public IReadOnlyList<ScoreEntry> Entries { get; }

    public string? ErrorMessage { get; }
}

/// <summary>仓储层统一异常基类，包装底层 SQLite 或 I/O 失败，供调用方决定降级策略。</summary>
public sealed class ScoreRepositoryException : Exception
{
    public ScoreRepositoryException(string message, Exception? inner = null) : base(message, inner) { }
}

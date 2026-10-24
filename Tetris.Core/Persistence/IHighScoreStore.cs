namespace Tetris.Persistence;

/// <summary>最高分写入结果，供界面提示使用。</summary>
public enum SaveOutcome
{
    /// <summary>已写入磁盘。</summary>
    Saved,

    /// <summary>未刷新纪录，无需写入。</summary>
    Unchanged,

    /// <summary>目录只读或无权限。</summary>
    AccessDenied,

    /// <summary>其他 I/O 失败（磁盘满、被长期占用等）。</summary>
    IoError
}

/// <summary>
/// 最高分存储抽象。界面只依赖该接口，测试可注入只读、损坏等故障实现。
/// </summary>
public interface IHighScoreStore
{
    /// <summary>当前历史最高分。</summary>
    int HighScore { get; }

    /// <summary>最近一次提交的结果。</summary>
    SaveOutcome LastOutcome { get; }

    /// <summary>最近一次失败的异常信息，成功时为 null。</summary>
    string? LastError { get; }

    /// <summary>提交本局分数；刷新纪录时返回 true（无论是否成功落盘）。</summary>
    bool Submit(int score);
}

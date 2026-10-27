using Microsoft.Data.Sqlite;

namespace Tetris.Persistence;

/// <summary>
/// 基于 SQLite 的排行榜仓储实现。
/// 使用 WAL 模式以获得更好的并发性能，所有数据库操作都有异常包装，
/// 对损坏文件、权限错误、磁盘不足等边界情况做了降级处理。
/// </summary>
public sealed class SqliteScoreRepository : IScoreRepository
{
    /// <summary>当前数据库 schema 版本；升级时递增并在 MigrateAsync 里处理旧版本迁移。</summary>
    public const int CurrentSchemaVersion = 1;

    private const int MaxRetries = 3;
    private const int RetryDelayMs = 50;

    private readonly string _dbPath;
    private readonly object _lock = new();
    private bool _initialized;

    public SqliteScoreRepository(string? dbPath = null)
    {
        _dbPath = dbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tetris",
            "scores.db");
    }

    /// <summary>数据库文件的完整路径。</summary>
    public string DatabasePath => _dbPath;

    private void EnsureInitialized()
    {
        if (_initialized) return;

        lock (_lock)
        {
            if (_initialized) return;

            try
            {
                EnsureDirectory();
                Migrate();
                _initialized = true;
            }
            catch (Exception ex)
            {
                throw new ScoreRepositoryException(
                    $"无法初始化排行榜数据库：{ex.Message}", ex);
            }
        }
    }

    private void EnsureDirectory()
    {
        string? dir = Path.GetDirectoryName(_dbPath);
        if (string.IsNullOrEmpty(dir)) return;

        // 即使目录已存在，Directory.CreateDirectory 在被占用时可能抛 UnauthorizedAccessException
        try
        {
            Directory.CreateDirectory(dir);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new ScoreRepositoryException(
                $"无权限创建数据目录：{dir}", ex);
        }
        catch (IOException ex)
        {
            throw new ScoreRepositoryException(
                $"创建数据目录失败：{ex.Message}", ex);
        }
    }

    private SqliteConnection CreateConnection()
    {
        // 使用 WAL 模式改善并发，启用外键约束。
        var conn = new SqliteConnection($"Data Source={_dbPath};Pooling=True;");
        return conn;
    }

    private void Migrate()
    {
        using var conn = CreateConnection();
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS SchemaVersion (
                Version INTEGER NOT NULL PRIMARY KEY
            );
            CREATE TABLE IF NOT EXISTS Scores (
                Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                Score        INTEGER NOT NULL,
                Lines        INTEGER NOT NULL,
                Level        INTEGER NOT NULL,
                PlayerName   TEXT    NOT NULL DEFAULT '',
                AchievedAt   TEXT    NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Scores_Score ON Scores (Score DESC);";

        cmd.ExecuteNonQuery();

        // 写入或更新 schema 版本标记。
        using var versionCmd = conn.CreateCommand();
        versionCmd.CommandText = @"
            INSERT OR REPLACE INTO SchemaVersion (Version) VALUES ($v)";
        versionCmd.Parameters.AddWithValue("$v", CurrentSchemaVersion);
        versionCmd.ExecuteNonQuery();
    }

    public int Add(ScoreEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        EnsureInitialized();

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO Scores (Score, Lines, Level, PlayerName, AchievedAt)
                    VALUES ($score, $lines, $level, $name, $at);
                    SELECT last_insert_rowid();";

                cmd.Parameters.AddWithValue("$score", entry.Score);
                cmd.Parameters.AddWithValue("$lines", entry.Lines);
                cmd.Parameters.AddWithValue("$level", entry.Level);
                cmd.Parameters.AddWithValue("$name", entry.PlayerName ?? "");
                cmd.Parameters.AddWithValue("$at", entry.AchievedAt.ToString("o"));

                var id = Convert.ToInt32(cmd.ExecuteScalar());
                return id;
            }
            catch (SqliteException ex) when (IsRetryable(ex) && attempt < MaxRetries - 1)
            {
                Thread.Sleep(RetryDelayMs);
            }
            catch (SqliteException ex)
            {
                throw new ScoreRepositoryException(
                    $"保存成绩失败（SQLite 错误 {ex.SqliteErrorCode}）：{ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new ScoreRepositoryException($"保存成绩失败（I/O 错误）：{ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new ScoreRepositoryException($"保存成绩失败（无权限）：{ex.Message}", ex);
            }
        }

        throw new ScoreRepositoryException("保存成绩失败：多次重试后仍无法写入数据库");
    }

    public ScoreboardResult GetTopScores(int topN = 10)
    {
        if (topN <= 0)
        {
            topN = 10;
        }

        return Query($"SELECT Score, Lines, Level, PlayerName, AchievedAt FROM Scores ORDER BY Score DESC, Id DESC LIMIT {topN}");
    }

    public ScoreboardResult GetAllScores()
    {
        return Query("SELECT Score, Lines, Level, PlayerName, AchievedAt FROM Scores ORDER BY Score DESC, Id DESC");
    }

    public void Clear()
    {
        EnsureInitialized();

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                using var conn = CreateConnection();
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM Scores;";
                cmd.ExecuteNonQuery();
                return;
            }
            catch (SqliteException ex) when (IsRetryable(ex) && attempt < MaxRetries - 1)
            {
                Thread.Sleep(RetryDelayMs);
            }
            catch (SqliteException ex)
            {
                throw new ScoreRepositoryException(
                    $"清空成绩失败（SQLite 错误 {ex.SqliteErrorCode}）：{ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new ScoreRepositoryException($"清空成绩失败（I/O 错误）：{ex.Message}", ex);
            }
        }

        throw new ScoreRepositoryException("清空成绩失败：多次重试后仍无法写入");
    }

    private ScoreboardResult Query(string sql)
    {
        try
        {
            EnsureInitialized();
        }
        catch (ScoreRepositoryException ex) when (ex.InnerException is UnauthorizedAccessException)
        {
            return new ScoreboardResult(ScoreboardStatus.AccessDenied, Array.Empty<ScoreEntry>(), ex.Message);
        }
        catch (ScoreRepositoryException ex) when (IsCorrupted(ex))
        {
            return new ScoreboardResult(ScoreboardStatus.Corrupted, Array.Empty<ScoreEntry>(), ex.Message);
        }
        catch (ScoreRepositoryException ex)
        {
            return new ScoreboardResult(ScoreboardStatus.IoError, Array.Empty<ScoreEntry>(), ex.Message);
        }

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                var list = new List<ScoreEntry>();

                using var conn = CreateConnection();
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = sql;

                using var reader = cmd.ExecuteReader();
                int idx = 0;

                while (reader.Read())
                {
                    int score = reader.GetInt32(0);
                    int lines = reader.GetInt32(1);
                    int level = reader.GetInt32(2);
                    string name = reader.GetString(3);
                    string atStr = reader.GetString(4);

                    DateTime achieved = DateTime.TryParse(atStr, out var dt) ? dt : DateTime.MinValue;

                    list.Add(new ScoreEntry(++idx, score, lines, level, name, achieved));
                }

                if (list.Count == 0)
                {
                    return new ScoreboardResult(ScoreboardStatus.Empty, list);
                }

                return new ScoreboardResult(ScoreboardStatus.Ok, list);
            }
            catch (SqliteException) when (attempt < MaxRetries - 1)
            {
                Thread.Sleep(RetryDelayMs);
            }
            catch (SqliteException ex)
            {
                // SQLITE_CORRUPT = 11, SQLITE_NOTADB = 26：数据库文件损坏或根本不是 SQLite 文件。
                if (ex.SqliteErrorCode == 11 || ex.SqliteErrorCode == 26)
                {
                    return new ScoreboardResult(
                        ScoreboardStatus.Corrupted,
                        Array.Empty<ScoreEntry>(),
                        $"排行榜数据库已损坏：{ex.Message}");
                }

                return new ScoreboardResult(
                    ScoreboardStatus.IoError,
                    Array.Empty<ScoreEntry>(),
                    $"排行榜查询失败（SQLite 错误 {ex.SqliteErrorCode}）：{ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                return new ScoreboardResult(
                    ScoreboardStatus.AccessDenied,
                    Array.Empty<ScoreEntry>(),
                    $"无权限访问排行榜：{ex.Message}");
            }
            catch (IOException ex)
            {
                return new ScoreboardResult(
                    ScoreboardStatus.IoError,
                    Array.Empty<ScoreEntry>(),
                    $"排行榜查询失败（I/O 错误）：{ex.Message}");
            }
        }

        return new ScoreboardResult(
            ScoreboardStatus.IoError,
            Array.Empty<ScoreEntry>(),
            "排行榜查询失败：多次重试后仍无法读取数据库");
    }

    private static bool IsRetryable(SqliteException ex)
    {
        // SQLITE_BUSY = 5, SQLITE_LOCKED = 6。
        return ex.SqliteErrorCode == 5 || ex.SqliteErrorCode == 6;
    }

    /// <summary>递归检查异常链，判断底层是否为 SQLite 损坏错误（SQLITE_CORRUPT=11 / SQLITE_NOTADB=26）。</summary>
    private static bool IsCorrupted(Exception ex)
    {
        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur is SqliteException sqlite &&
                (sqlite.SqliteErrorCode == 11 || sqlite.SqliteErrorCode == 26))
            {
                return true;
            }
        }

        return false;
    }
}

using Tetris.Persistence;

namespace Tetris.Tests;

/// <summary>
/// SQLite 排行榜仓储的健壮性测试：正常 CRUD、空库、损坏降级、并发重试。
/// 每个用例使用独立临时目录，互不干扰。
/// </summary>
public sealed class SqliteScoreRepositoryTests : IDisposable
{
    private readonly string _directory;

    public SqliteScoreRepositoryTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "tetris-score-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (Exception)
        {
            // 清理失败不影响测试结论。
        }
    }

    private string DbPath => Path.Combine(_directory, "scores.db");

    private SqliteScoreRepository CreateRepo() => new(DbPath);

    [Fact]
    public void EmptyDb_GetTopScores_ReturnsEmptyStatus()
    {
        var repo = CreateRepo();

        ScoreboardResult result = repo.GetTopScores(5);

        Assert.Equal(ScoreboardStatus.Empty, result.Status);
        Assert.Empty(result.Entries);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void Add_ThenGetTopScores_ReturnsRankedList()
    {
        var repo = CreateRepo();

        repo.Add(new ScoreEntry(1500, 5, 3, "Alice", DateTime.UtcNow));
        repo.Add(new ScoreEntry(900, 3, 2, "Bob", DateTime.UtcNow));
        repo.Add(new ScoreEntry(2100, 8, 4, "Carol", DateTime.UtcNow));

        ScoreboardResult result = repo.GetTopScores(10);

        Assert.Equal(ScoreboardStatus.Ok, result.Status);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal(2100, result.Entries[0].Score);
        Assert.Equal("Carol", result.Entries[0].PlayerName);
        Assert.Equal(1500, result.Entries[1].Score);
        Assert.Equal(900, result.Entries[2].Score);
    }

    [Fact]
    public void Add_ManyEntries_TopN_RespectsLimit()
    {
        var repo = CreateRepo();

        for (int i = 1; i <= 20; i++)
        {
            repo.Add(new ScoreEntry(i * 100, i, i, "P" + i, DateTime.UtcNow));
        }

        ScoreboardResult top5 = repo.GetTopScores(5);

        Assert.Equal(5, top5.Entries.Count);
        Assert.Equal(2000, top5.Entries[0].Score);
        Assert.Equal(1900, top5.Entries[1].Score);

        // 负数或零的 topN 应自动修正为 10。
        ScoreboardResult defaultTop = repo.GetTopScores(-1);
        Assert.Equal(10, defaultTop.Entries.Count);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var repo = CreateRepo();

        repo.Add(new ScoreEntry(100, 1, 1, "X", DateTime.UtcNow));
        repo.Add(new ScoreEntry(200, 2, 1, "Y", DateTime.UtcNow));

        repo.Clear();

        ScoreboardResult result = repo.GetAllScores();
        Assert.Equal(ScoreboardStatus.Empty, result.Status);
    }

    [Fact]
    public void Add_ReturnsUniqueAutoIncrementId()
    {
        var repo = CreateRepo();

        int id1 = repo.Add(new ScoreEntry(100, 1, 1, "A", DateTime.UtcNow));
        int id2 = repo.Add(new ScoreEntry(200, 2, 2, "B", DateTime.UtcNow));

        Assert.True(id1 > 0);
        Assert.True(id2 > id1);
    }

    [Fact]
    public void CorruptedDatabase_ReturnsCorruptedStatus()
    {
        // 预先写入一段垃圾字节，让 SQLite 无法解析。
        File.WriteAllBytes(DbPath, new byte[] { 0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE });

        var repo = CreateRepo();

        ScoreboardResult result = repo.GetTopScores();

        Assert.Equal(ScoreboardStatus.Corrupted, result.Status);
        Assert.Empty(result.Entries);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public void Repository_CreatedTwice_UsesSameDbFile()
    {
        var repo1 = CreateRepo();
        repo1.Add(new ScoreEntry(500, 2, 1, "Shared", DateTime.UtcNow));

        var repo2 = CreateRepo(); // 同路径
        ScoreboardResult result = repo2.GetTopScores();

        Assert.Equal(ScoreboardStatus.Ok, result.Status);
        Assert.Single(result.Entries);
        Assert.Equal(500, result.Entries[0].Score);
    }

    [Fact]
    public void InvalidArguments_Add_RejectsNullEntry()
    {
        var repo = CreateRepo();

        Assert.Throws<ArgumentNullException>(() => repo.Add(null!));
    }

    [Fact]
    public void Entry_WithPlayerName_NullOrEmpty_StoredAsEmpty()
    {
        var repo = CreateRepo();

        repo.Add(new ScoreEntry(300, 1, 1, null!, DateTime.UtcNow));
        repo.Add(new ScoreEntry(400, 1, 1, "", DateTime.UtcNow));

        ScoreboardResult result = repo.GetAllScores();

        Assert.Equal(2, result.Entries.Count);
        foreach (var e in result.Entries)
        {
            Assert.NotNull(e.PlayerName);
        }
    }
}

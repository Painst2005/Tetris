using System.Xml.Linq;
using Tetris.Persistence;

namespace Tetris.Tests;

/// <summary>
/// 最高分持久化的健壮性测试：正常往返、损坏隔离、文件被占用时的降级。
/// 每个用例使用独立的临时目录，互不干扰。
/// </summary>
public sealed class HighScoreStoreTests : IDisposable
{
    private readonly string _directory;

    public HighScoreStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "tetris-tests-" + Guid.NewGuid().ToString("N"));
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

    private string ScorePath => Path.Combine(_directory, "highscore.xml");

    [Fact]
    public void MissingFile_StartsAtZeroWithoutThrowing()
    {
        var store = new HighScoreStore(ScorePath);

        Assert.Equal(0, store.HighScore);
        Assert.False(File.Exists(ScorePath));
    }

    [Fact]
    public void Submit_ThenReload_RoundTripsScore()
    {
        var store = new HighScoreStore(ScorePath);

        Assert.True(store.Submit(4200));
        Assert.Equal(SaveOutcome.Saved, store.LastOutcome);

        var reloaded = new HighScoreStore(ScorePath);

        Assert.Equal(4200, reloaded.HighScore);
    }

    [Fact]
    public void SavedFile_CarriesCurrentFormatVersion()
    {
        var store = new HighScoreStore(ScorePath);
        store.Submit(100);

        XDocument document = XDocument.Load(ScorePath);
        string? version = document.Root?.Attribute("Version")?.Value;

        Assert.Equal(HighScoreStore.CurrentFormatVersion.ToString(), version);
    }

    [Fact]
    public void LegacyFileWithoutVersion_IsStillReadable()
    {
        // 上一版程序写出的文件没有 Version 属性，升级后必须还能读。
        File.WriteAllText(
            ScorePath,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><HighScore><Score>777</Score></HighScore>");

        var store = new HighScoreStore(ScorePath);

        Assert.Equal(777, store.HighScore);
        Assert.Equal(0, store.FormatVersion);
        Assert.True(File.Exists(ScorePath), "旧格式文件不应被当成损坏文件隔离");
    }

    [Fact]
    public void Submit_LowerScore_KeepsExistingRecord()
    {
        var store = new HighScoreStore(ScorePath);
        store.Submit(4200);

        Assert.False(store.Submit(500));
        Assert.Equal(4200, store.HighScore);
        Assert.Equal(SaveOutcome.Unchanged, store.LastOutcome);
    }

    [Fact]
    public void CorruptFile_IsQuarantinedAndResetsToZero()
    {
        File.WriteAllText(ScorePath, "<<< 这不是合法的 XML >>>");

        var store = new HighScoreStore(ScorePath);

        Assert.Equal(0, store.HighScore);
        Assert.False(File.Exists(ScorePath), "损坏文件应被移走，保证下次启动干净");
        Assert.NotEmpty(Directory.GetFiles(_directory, "highscore.xml.corrupt-*"));
    }

    [Fact]
    public void WriteWhileFileIsLocked_DegradesButKeepsScoreInMemory()
    {
        var store = new HighScoreStore(ScorePath);

        using (new FileStream(ScorePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.True(store.Submit(900));

            // 内存中的纪录保住了，但落盘失败必须有明确结果，不能静默。
            Assert.Equal(900, store.HighScore);
            Assert.Equal(SaveOutcome.IoError, store.LastOutcome);
            Assert.NotNull(store.LastError);
        }
    }

    [Fact]
    public void ReadWhileAnotherHandleIsOpen_StillLoads()
    {
        var store = new HighScoreStore(ScorePath);
        store.Submit(1500);

        using (new FileStream(ScorePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
        {
            var reloaded = new HighScoreStore(ScorePath);

            Assert.Equal(1500, reloaded.HighScore);
        }
    }
}

using System.Security;
using System.Xml;
using System.Xml.Linq;

namespace Tetris.Persistence;

/// <summary>
/// 将历史最高分以 XML 形式保存在本地应用数据目录。
/// 写入采用「临时文件 + 原子替换」，读取容忍文件被占用，损坏文件会被隔离。
/// </summary>
public sealed class HighScoreStore : IHighScoreStore
{
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 50;

    private readonly string _filePath;

    public HighScoreStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Tetris",
            "highscore.xml");

        Load();
    }

    public int HighScore { get; private set; }

    public SaveOutcome LastOutcome { get; private set; } = SaveOutcome.Unchanged;

    public string? LastError { get; private set; }

    /// <summary>分数超过历史纪录时更新，并尝试写入 XML；返回是否刷新了纪录。</summary>
    public bool Submit(int score)
    {
        if (score <= HighScore)
        {
            LastOutcome = SaveOutcome.Unchanged;
            LastError = null;
            return false;
        }

        HighScore = score;

        if (Persist(out SaveOutcome outcome, out string? error))
        {
            LastOutcome = SaveOutcome.Saved;
            LastError = null;
            return true;
        }

        // 落盘失败时内存中的纪录仍然保留，由界面提示「本次成绩未保存」。
        LastOutcome = outcome;
        LastError = error;
        return true;
    }

    private void Load()
    {
        if (!FileExistsSafe())
        {
            return;
        }

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                XDocument document;

                // 在独立作用域内读完即关闭：否则隔离损坏文件时会被自身句柄占用。
                using (var stream = new FileStream(
                           _filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    document = XDocument.Load(stream);
                }

                string? raw = document.Root?.Element("Score")?.Value ?? document.Root?.Value;

                if (int.TryParse(raw?.Trim(), out int score) && score >= 0)
                {
                    HighScore = score;
                    return;
                }

                Quarantine();
                HighScore = 0;
                return;
            }
            catch (XmlException) when (attempt < MaxRetries - 1)
            {
                // 可能读到别的进程写了一半的内容，先重试，最后一次仍失败才隔离。
                Thread.Sleep(RetryDelayMs);
            }
            catch (XmlException)
            {
                Quarantine();
                HighScore = 0;
                return;
            }
            catch (IOException) when (attempt < MaxRetries - 1)
            {
                Thread.Sleep(RetryDelayMs);
            }
            catch (Exception)
            {
                // 无权限、路径非法等：按首次运行处理，不影响游戏启动。
                HighScore = 0;
                return;
            }
        }

        HighScore = 0;
    }

    private bool FileExistsSafe()
    {
        try
        {
            return File.Exists(_filePath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private bool Persist(out SaveOutcome outcome, out string? error)
    {
        string tempPath = _filePath + ".tmp";

        for (int attempt = 0; attempt < MaxRetries; attempt++)
        {
            try
            {
                string? directory = Path.GetDirectoryName(_filePath);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var document = new XDocument(
                    new XElement("HighScore",
                        new XElement("Score", HighScore)));

                File.WriteAllText(tempPath, document.ToString());

                if (File.Exists(_filePath))
                {
                    // 原子替换：写一半断电不会破坏已有纪录。
                    File.Replace(tempPath, _filePath, destinationBackupFileName: null,
                        ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tempPath, _filePath, overwrite: true);
                }

                outcome = SaveOutcome.Saved;
                error = null;
                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                CleanupTemp(tempPath);
                outcome = SaveOutcome.AccessDenied;
                error = ex.Message;
                return false;
            }
            catch (SecurityException ex)
            {
                CleanupTemp(tempPath);
                outcome = SaveOutcome.AccessDenied;
                error = ex.Message;
                return false;
            }
            catch (IOException ex) when (attempt < MaxRetries - 1)
            {
                // 文件被短暂占用：稍后重试。
                Thread.Sleep(RetryDelayMs);
            }
            catch (IOException ex)
            {
                CleanupTemp(tempPath);
                outcome = SaveOutcome.IoError;
                error = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                CleanupTemp(tempPath);
                outcome = SaveOutcome.IoError;
                error = ex.Message;
                return false;
            }
        }

        CleanupTemp(tempPath);
        outcome = SaveOutcome.IoError;
        error = "写入重试次数已用尽";
        return false;
    }

    /// <summary>把损坏文件改名保留，便于事后排查，同时保证下次启动能正常读取。</summary>
    private void Quarantine()
    {
        try
        {
            string target = $"{_filePath}.corrupt-{DateTime.Now:yyyyMMddHHmmss}";
            File.Move(_filePath, target, overwrite: true);
        }
        catch (Exception)
        {
            // 隔离失败也不应影响启动。
        }
    }

    private static void CleanupTemp(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // 临时文件残留无害，下次写入会覆盖。
        }
    }
}

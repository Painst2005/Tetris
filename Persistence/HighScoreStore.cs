using System.Xml.Linq;

namespace Tetris.Persistence;

/// <summary>
/// 将历史最高分以 XML 形式保存在本地应用数据目录。
/// </summary>
public sealed class HighScoreStore
{
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

    /// <summary>分数超过历史纪录时更新并写入 XML，返回是否刷新了纪录。</summary>
    public bool Submit(int score)
    {
        if (score <= HighScore)
        {
            return false;
        }

        HighScore = score;
        Save();
        return true;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return;
            }

            XDocument document = XDocument.Load(_filePath);
            string? value = document.Root?.Element("Score")?.Value;

            if (int.TryParse(value, out int score) && score >= 0)
            {
                HighScore = score;
            }
        }
        catch (Exception)
        {
            // 文件损坏或不可读时按 0 分处理，避免影响游戏启动。
            HighScore = 0;
        }
    }

    private void Save()
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

            document.Save(_filePath);
        }
        catch (Exception)
        {
            // 写入失败时保留内存中的纪录，不影响当前游戏。
        }
    }
}

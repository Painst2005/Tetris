using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Tetris.Game;
using Tetris.Persistence;

namespace Tetris.UI;

public sealed class MainForm : Form
{
    private const int DesignWidth = 700;
    private const int DesignHeight = 700;
    private const int CellSize = 32;
    private const int BoardX = 28;
    private const int BoardY = 88;
    private const int SideX = 444;

    private static readonly RectangleF DesignRect = new(0, 0, DesignWidth, DesignHeight);

    private static readonly Rectangle BoardRect =
        new(BoardX, BoardY, TetrisGame.Columns * CellSize, TetrisGame.Rows * CellSize);

    private static readonly SolidBrush LabelBrush = new(Color.FromArgb(124, 137, 168));
    private static readonly SolidBrush ValueBrush = new(Color.FromArgb(230, 237, 247));
    private static readonly SolidBrush ScoreBrush = new(Color.FromArgb(103, 232, 249));
    private static readonly SolidBrush MutedBrush = new(Color.FromArgb(93, 107, 138));
    private static readonly SolidBrush KeyBrush = new(Color.FromArgb(125, 211, 252));
    private static readonly SolidBrush DescBrush = new(Color.FromArgb(154, 167, 199));
    private static readonly SolidBrush RecordBrush = new(Color.FromArgb(251, 191, 36));

    private readonly TetrisGame _game = new();
    private readonly IHighScoreStore _highScoreStore = new HighScoreStore();
    private readonly IScoreRepository _scoreRepository = new SqliteScoreRepository();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly HashSet<Keys> _pressed = new();

    private ScoreboardResult? _topScores;
    private long _scoresLoadedAt = -1;

    private readonly Font _titleFont = PixelFont("Segoe UI", 40f, FontStyle.Bold);
    private readonly Font _subtitleFont = PixelFont("Segoe UI", 12f);
    private readonly Font _labelFont = PixelFont("Segoe UI", 12f);
    private readonly Font _scoreFont = PixelFont("Consolas", 32f, FontStyle.Bold);
    private readonly Font _statFont = PixelFont("Consolas", 29f, FontStyle.Bold);
    private readonly Font _keyFont = PixelFont("Consolas", 12f, FontStyle.Bold);
    private readonly Font _recordFont = PixelFont("Segoe UI", 11f, FontStyle.Bold);
    private readonly Font _overlayTitleFont = PixelFont("Segoe UI", 33f, FontStyle.Bold);
    private readonly Font _overlayBodyFont = PixelFont("Segoe UI", 14f);

    private long _lastTick;
    private long _nextLeftRepeat;
    private long _nextRightRepeat;
    private long _nextDownRepeat;
    private GameState _lastState = GameState.Running;
    private string? _notice;
    private long _noticeUntil;

    // 动画状态：锁定辉光、消行闪白、平滑下落、界面过渡与数值反馈。
    private readonly List<LockFlash> _lockFlashes = new();
    private readonly List<ClearFlash> _clearFlashes = new();
    private Tetromino? _renderPiece;
    private float _pieceRenderY;
    private long _gameOverSince = -1;
    private long _startedAt = -1;
    private int _lastScore;
    private int _lastLevel = 1;
    private int _scoreDelta;
    private long _scorePopAt = -1;
    private long _levelUpAt = -1;

    public MainForm()
    {
        Text = "Tetris · 俄罗斯方块";
        ClientSize = new Size(DesignWidth, DesignHeight);
        MinimumSize = new Size(480, 460);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(5, 8, 16);
        DoubleBuffered = true;
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);

        _game.PieceLocked += OnPieceLocked;
        _game.LinesCleared += OnLinesCleared;

        _timer = new System.Windows.Forms.Timer { Interval = 16 };
        _timer.Tick += OnGameTick;
        _lastTick = _clock.ElapsedMilliseconds;
        _timer.Start();

        // 启动时预加载排行榜，失败不影响游戏。
        try
        {
            _topScores = _scoreRepository.GetTopScores(10);
            _scoresLoadedAt = 0;
        }
        catch (ScoreRepositoryException)
        {
            _topScores = new ScoreboardResult(ScoreboardStatus.IoError, Array.Empty<ScoreEntry>(), "排行榜不可用");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _titleFont.Dispose();
            _subtitleFont.Dispose();
            _labelFont.Dispose();
            _scoreFont.Dispose();
            _statFont.Dispose();
            _keyFont.Dispose();
            _recordFont.Dispose();
            _overlayTitleFont.Dispose();
            _overlayBodyFont.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // 按当前 DPI 给出合适的初始尺寸，但不限制之后的手动缩放。
        float dpiScale = DeviceDpi / 96f;
        Rectangle working = Screen.FromControl(this).WorkingArea;

        int width = Math.Min((int)Math.Round(DesignWidth * dpiScale), working.Width - 80);
        int height = Math.Min((int)Math.Round(DesignHeight * dpiScale), working.Height - 80);

        ClientSize = new Size(
            Math.Max(width, MinimumSize.Width),
            Math.Max(height, MinimumSize.Height));
        CenterToScreen();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        _pressed.Clear();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SubmitScore();
        base.OnFormClosing(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left || _showHistoryButtonRect.IsEmpty) return;

        // 设计坐标转物理坐标：逆用 OnPaint 里的 scale/offset。
        float scale = Math.Min(
            Math.Max(ClientSize.Width, 1) / (float)DesignWidth,
            Math.Max(ClientSize.Height, 1) / (float)DesignHeight);
        float offsetX = (ClientSize.Width - DesignWidth * scale) / 2f;
        float offsetY = (ClientSize.Height - DesignHeight * scale) / 2f;

        var point = new PointF(
            (e.X - offsetX) / scale,
            (e.Y - offsetY) / scale);

        if (_showHistoryButtonRect.Contains(point))
        {
            ShowHistoryDialog();
        }
    }

    private void ShowHistoryDialog()
    {
        ScoreboardResult result;
        try
        {
            result = _scoreRepository.GetAllScores();
        }
        catch (ScoreRepositoryException ex)
        {
            MessageBox.Show(
                $"无法读取历史记录：{ex.Message}",
                "排行榜",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        using var dialog = new Form
        {
            Text = "历史全部成绩",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Color.FromArgb(5, 8, 16),
            ForeColor = Color.White,
            ClientSize = new Size(520, 420)
        };

        var title = new Label
        {
            Text = "全部历史成绩  ·  按分数降序",
            ForeColor = Color.FromArgb(251, 191, 36),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(16, 16)
        };
        dialog.Controls.Add(title);

        var closeBtn = new Button
        {
            Text = "关闭",
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(28, 54, 82),
            ForeColor = Color.White,
            Location = new Point(430, 376),
            Size = new Size(76, 28)
        };
        closeBtn.FlatAppearance.BorderColor = Color.FromArgb(71, 85, 105);
        dialog.Controls.Add(closeBtn);
        dialog.AcceptButton = closeBtn;

        var list = new ListBox
        {
            Location = new Point(16, 48),
            Size = new Size(488, 320),
            Font = new Font("Consolas", 10f),
            IntegralHeight = false,
            BackColor = Color.FromArgb(10, 15, 28),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            ScrollAlwaysVisible = true
        };

        string[] headers = { "#", "玩家", "分数", "消行", "等级", "时间" };
        list.Items.Add(string.Format("{0,-3} {1,-14} {2,8} {3,6} {4,6}  {5}",
            headers[0], headers[1], headers[2], headers[3], headers[4], headers[5]));

        if (result.Status == ScoreboardStatus.Ok && result.Entries.Count > 0)
        {
            for (int i = 0; i < result.Entries.Count; i++)
            {
                ScoreEntry e = result.Entries[i];
                string name = e.PlayerName.Length > 12
                    ? e.PlayerName.AsSpan(0, 11).ToString() + "…"
                    : e.PlayerName;
                list.Items.Add(string.Format("{0,-3} {1,-14} {2,8} {3,6} {4,6}  {5}",
                    i + 1,
                    name,
                    $"0x{e.Score:X4}",
                    e.Lines,
                    e.Level,
                    e.AchievedAt.ToString("yyyy-MM-dd HH:mm")));
            }
        }
        else if (result.Status == ScoreboardStatus.Empty)
        {
            list.Items.Add("（暂无记录）");
        }
        else
        {
            list.Items.Add($"（无法加载：{result.ErrorMessage}）");
        }

        dialog.Controls.Add(list);
        dialog.ShowDialog(this);
    }

    /// <summary>提交本局分数；同时写入 XML 最高分和 SQLite 历史排行榜。</summary>
    private void SubmitScore()
    {
        bool isNewRecord = _highScoreStore.Submit(_game.Score);

        switch (_highScoreStore.LastOutcome)
        {
            case SaveOutcome.AccessDenied:
                ShowNotice("本次成绩未保存到本地（目录不可写）");
                break;

            case SaveOutcome.IoError:
                ShowNotice("本次成绩未保存到本地（磁盘写入失败）");
                break;
        }

        // 同时存入 SQLite 排行榜（仅当有实际成绩时，过滤掉刚启动就关的 0 分记录）。
        if (_game.Score > 0 || _game.Lines > 0)
        {
            try
            {
                string playerName = Environment.UserName ?? "Player";
                _scoreRepository.Add(new ScoreEntry(
                    _game.Score,
                    _game.Lines,
                    _game.Level,
                    playerName,
                    DateTime.Now));

                _topScores = _scoreRepository.GetTopScores(10);
                _scoresLoadedAt = _clock.ElapsedMilliseconds;
            }
            catch (ScoreRepositoryException ex)
            {
                ShowNotice($"排行榜保存失败：{ex.Message}");
            }
        }
    }

    private void ShowNotice(string message)
    {
        _notice = message;
        _noticeUntil = _clock.ElapsedMilliseconds + 6000;
    }

    private void OnPieceLocked(IReadOnlyList<(int X, int Y)> cells, TetrominoType type)
    {
        long now = _clock.ElapsedMilliseconds;
        Color color = BlockColor(type);

        foreach ((int x, int y) in cells)
        {
            _lockFlashes.Add(new LockFlash { X = x, Y = y, Color = color, Start = now });
        }
    }

    private void OnLinesCleared(IReadOnlyList<ClearedRow> rows)
    {
        long now = _clock.ElapsedMilliseconds;
        var clearedRows = new HashSet<int>();

        foreach (ClearedRow row in rows)
        {
            clearedRows.Add(row.Row);
            _clearFlashes.Add(new ClearFlash { Row = row.Row, Cells = row.Cells, Start = now });
        }

        // 被消除的行不再叠加锁定辉光，交给消行闪白表现。
        _lockFlashes.RemoveAll(flash => clearedRows.Contains(flash.Y));
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        // 准备界面：任意键开始游戏。
        if (_game.State == GameState.Ready)
        {
            _game.Start();
            e.Handled = true;
            e.SuppressKeyPress = true;
            Invalidate();
            return;
        }

        if (!_pressed.Add(e.KeyCode))
        {
            return;
        }

        long now = _clock.ElapsedMilliseconds;
        bool handled = true;

        switch (e.KeyCode)
        {
            case Keys.Left:
                _game.MoveLeft();
                _nextLeftRepeat = now + 150;
                break;

            case Keys.Right:
                _game.MoveRight();
                _nextRightRepeat = now + 150;
                break;

            case Keys.Down:
                _game.SoftDrop();
                _nextDownRepeat = now + 100;
                break;

            case Keys.Up:
                _game.RotateClockwise();
                break;

            case Keys.Z:
                _game.RotateCounterClockwise();
                break;

            case Keys.Space:
                _game.HardDrop();
                break;

            case Keys.P:
                _game.TogglePause();
                break;

            case Keys.R:
                _game.Reset();
                _game.Start();
                break;

            default:
                handled = false;
                break;
        }

        if (!handled)
        {
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        Invalidate();
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        _pressed.Remove(e.KeyCode);
        base.OnKeyUp(e);
    }

    private void OnGameTick(object? sender, EventArgs e)
    {
        long now = _clock.ElapsedMilliseconds;
        int elapsed = (int)Math.Clamp(now - _lastTick, 0, 100);
        _lastTick = now;

        _game.Update(elapsed);

        if (_game.State == GameState.Running)
        {
            if (_pressed.Contains(Keys.Left) && now >= _nextLeftRepeat)
            {
                _game.MoveLeft();
                _nextLeftRepeat = now + 45;
            }

            if (_pressed.Contains(Keys.Right) && now >= _nextRightRepeat)
            {
                _game.MoveRight();
                _nextRightRepeat = now + 45;
            }

            if (_pressed.Contains(Keys.Down) && now >= _nextDownRepeat)
            {
                _game.SoftDrop();
                _nextDownRepeat = now + 45;
            }
        }

        // 只在结束一局时写入，避免每帧重复落盘。
        if (_game.State == GameState.GameOver && _lastState != GameState.GameOver)
        {
            SubmitScore();
        }

        UpdateAnimations(now, elapsed);

        _lastState = _game.State;

        Invalidate();
    }

    /// <summary>推进所有视觉动画状态：平滑下落、分数弹跳、升级辉光与界面过渡时间戳。</summary>
    private void UpdateAnimations(long now, int elapsed)
    {
        if (_game.State == GameState.GameOver && _lastState != GameState.GameOver)
        {
            _gameOverSince = now;
        }
        else if (_game.State != GameState.GameOver)
        {
            _gameOverSince = -1;
        }

        if (_game.State == GameState.Running &&
            _lastState is GameState.Ready or GameState.GameOver)
        {
            _startedAt = now;
        }

        if (_game.State == GameState.Ready)
        {
            _lockFlashes.Clear();
            _clearFlashes.Clear();
            _startedAt = -1;
        }

        if (_game.Score != _lastScore)
        {
            if (_game.Score > _lastScore)
            {
                _scoreDelta = _game.Score - _lastScore;
                _scorePopAt = now;
            }

            _lastScore = _game.Score;
        }

        if (_game.Level != _lastLevel)
        {
            if (_game.Level > _lastLevel)
            {
                _levelUpAt = now;
            }

            _lastLevel = _game.Level;
        }

        // 当前方块纵向平滑跟随：普通下落时缓动，硬降 / 新方块 / 上踢直接吸附。
        if (!ReferenceEquals(_renderPiece, _game.Current) || _game.State != GameState.Running)
        {
            _renderPiece = _game.Current;
            _pieceRenderY = _game.CurrentY;
        }
        else
        {
            float diff = _game.CurrentY - _pieceRenderY;

            if (diff is > 3f or < 0f)
            {
                _pieceRenderY = _game.CurrentY;
            }
            else
            {
                _pieceRenderY += diff * Math.Min(1f, elapsed / 80f);

                if (Math.Abs(_game.CurrentY - _pieceRenderY) < 0.02f)
                {
                    _pieceRenderY = _game.CurrentY;
                }
            }
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        // 画面按客户区等比缩放并居中：窗口任意拉伸都不会溢出或错位。
        float scale = Math.Min(
            Math.Max(ClientSize.Width, 1) / (float)DesignWidth,
            Math.Max(ClientSize.Height, 1) / (float)DesignHeight);
        scale = Math.Max(scale, 0.25f);

        float offsetX = (ClientSize.Width - DesignWidth * scale) / 2f;
        float offsetY = (ClientSize.Height - DesignHeight * scale) / 2f;

        GraphicsState view = g.Save();
        g.TranslateTransform(offsetX, offsetY);
        g.ScaleTransform(scale, scale);
        g.SetClip(new RectangleF(0f, 0f, DesignWidth, DesignHeight));

        DrawBackground(g);
        DrawHeader(g);
        DrawBoard(g);
        DrawSidePanel(g);
        DrawFooter(g);

        g.Restore(view);
    }

    private void DrawBackground(Graphics g)
    {
        using var background = new LinearGradientBrush(
            DesignRect,
            Color.FromArgb(11, 16, 32),
            Color.FromArgb(5, 8, 16),
            90f);
        g.FillRectangle(background, DesignRect);

        using var cyanGlow = new SolidBrush(Color.FromArgb(12, 34, 211, 238));
        g.FillEllipse(cyanGlow, new RectangleF(470, -190, 460, 460));

        using var purpleGlow = new SolidBrush(Color.FromArgb(10, 167, 139, 250));
        g.FillEllipse(purpleGlow, new RectangleF(-220, 380, 480, 480));
    }

    private void DrawHeader(Graphics g)
    {
        using var accentPath = RoundedRectangle(new RectangleF(28, 21, 8, 42), 4);
        using var accentBrush = new LinearGradientBrush(
            new RectangleF(28, 21, 8, 42),
            Color.FromArgb(34, 211, 238),
            Color.FromArgb(167, 139, 250),
            90f);
        g.FillPath(accentBrush, accentPath);

        using var titleBrush = new LinearGradientBrush(
            new RectangleF(44, 12, 260, 52),
            Color.FromArgb(103, 232, 249),
            Color.FromArgb(167, 139, 250),
            0f);
        g.DrawString("TETRIS", _titleFont, titleBrush, 44f, 12f);

        g.DrawString("12 × 16 · 经典俄罗斯方块", _subtitleFont, LabelBrush, 47f, 62f);

        using var dividerPen = new Pen(Color.FromArgb(35, 54, 82), 1f);
        g.DrawLine(dividerPen, 28f, 80f, 664f, 80f);
    }

    private void DrawBoard(Graphics g)
    {
        using var boardPath = RoundedRectangle(BoardRect, 12);

        using (var boardBrush = new LinearGradientBrush(
                   BoardRect,
                   Color.FromArgb(9, 14, 27),
                   Color.FromArgb(5, 8, 18),
                   90f))
        {
            g.FillPath(boardBrush, boardPath);
        }

        GraphicsState state = g.Save();
        g.SetClip(boardPath, CombineMode.Intersect);

        // 棋盘中心轻微提亮，让网格与背景融合得更自然。
        using var centerGlow = new SolidBrush(Color.FromArgb(7, 148, 197, 255));
        g.FillEllipse(
            centerGlow,
            new RectangleF(
                BoardRect.X + 48f,
                BoardRect.Y + 128f,
                BoardRect.Width - 96f,
                BoardRect.Height - 256f));

        DrawGrid(g);
        DrawLockedBlocks(g);
        DrawLockFlashes(g);
        DrawClearFlashes(g);
        DrawGhostPiece(g);
        DrawCurrentPiece(g);

        g.Restore(state);

        // 一圈极淡的青色外发光，呼应整体霓虹氛围。
        using var outerGlowPen = new Pen(Color.FromArgb(13, 34, 211, 238), 3f);
        g.DrawPath(outerGlowPen, boardPath);

        using var borderPen = new Pen(Color.FromArgb(52, 62, 96), 1.4f);
        g.DrawPath(borderPen, boardPath);

        if (_game.State == GameState.Ready)
        {
            DrawStartOverlay(g);
        }
        else if (_game.State == GameState.Paused)
        {
            DrawPauseOverlay(g);
        }
        else if (_game.State == GameState.GameOver)
        {
            DrawGameOverOverlay(g);
        }
        else if (_startedAt >= 0)
        {
            // 开始游戏后让准备界面短暂淡出，而不是瞬间消失。
            float t = (_clock.ElapsedMilliseconds - _startedAt) / 260f;

            if (t < 1f)
            {
                DrawStartOverlay(g, 1f - t);
            }
            else
            {
                _startedAt = -1;
            }
        }
    }

    private static void DrawGrid(Graphics g)
    {
        using var gridPen = new Pen(Color.FromArgb(19, 148, 178, 210), 1f);

        for (int col = 1; col < TetrisGame.Columns; col++)
        {
            float x = BoardX + col * CellSize;
            g.DrawLine(gridPen, x, BoardY, x, BoardY + BoardRect.Height);
        }

        for (int row = 1; row < TetrisGame.Rows; row++)
        {
            float y = BoardY + row * CellSize;
            g.DrawLine(gridPen, BoardX, y, BoardX + BoardRect.Width, y);
        }
    }

    private void DrawLockedBlocks(Graphics g)
    {
        for (int row = 0; row < TetrisGame.Rows; row++)
        {
            for (int col = 0; col < TetrisGame.Columns; col++)
            {
                TetrominoType? type = _game.GetCell(col, row);

                if (type is null)
                {
                    continue;
                }

                DrawBlock(g, CellRectangle(col, row), BlockColor(type.Value));
            }
        }
    }

    /// <summary>方块锁定瞬间的辉光：外扩一圈微光并快速衰减。</summary>
    private void DrawLockFlashes(Graphics g)
    {
        if (_lockFlashes.Count == 0)
        {
            return;
        }

        long now = _clock.ElapsedMilliseconds;
        const float duration = 200f;

        for (int i = _lockFlashes.Count - 1; i >= 0; i--)
        {
            LockFlash flash = _lockFlashes[i];
            float t = (now - flash.Start) / duration;

            if (t >= 1f)
            {
                _lockFlashes.RemoveAt(i);
                continue;
            }

            float fade = 1f - t;
            RectangleF rect = CellRectangle(flash.X, flash.Y);
            rect.Inflate(2f + 3f * fade, 2f + 3f * fade);

            using var path = RoundedRectangle(rect, 7f);
            using var glow = new SolidBrush(Color.FromArgb(ScaleAlpha(70, fade), flash.Color));
            g.FillPath(glow, path);

            using var pen = new Pen(
                Color.FromArgb(ScaleAlpha(150, fade), ControlPaint.Light(flash.Color, 0.5f)),
                1.4f);
            g.DrawPath(pen, path);
        }
    }

    /// <summary>消行动画：整行先闪白，再按原方块颜色渐隐。</summary>
    private void DrawClearFlashes(Graphics g)
    {
        if (_clearFlashes.Count == 0)
        {
            return;
        }

        long now = _clock.ElapsedMilliseconds;
        const float duration = 340f;

        for (int i = _clearFlashes.Count - 1; i >= 0; i--)
        {
            ClearFlash flash = _clearFlashes[i];
            float t = (now - flash.Start) / duration;

            if (t >= 1f)
            {
                _clearFlashes.RemoveAt(i);
                continue;
            }

            float fade = t < 0.35f ? 1f : 1f - (t - 0.35f) / 0.65f;
            float whiten = Math.Max(0f, 1f - t / 0.35f);

            for (int col = 0; col < TetrisGame.Columns; col++)
            {
                Color color = Mix(Color.White, BlockColor(flash.Cells[col]), 1f - whiten);
                using var path = RoundedRectangle(CellRectangle(col, flash.Row), 6f);
                using var brush = new SolidBrush(Color.FromArgb(ScaleAlpha(215, fade), color));
                g.FillPath(brush, path);
            }
        }
    }

    private void DrawGhostPiece(Graphics g)
    {
        if (_game.State != GameState.Running)
        {
            return;
        }

        int ghostY = _game.GhostY;

        if (ghostY == _game.CurrentY)
        {
            return;
        }

        Tetromino piece = _game.Current;
        Color color = BlockColor(piece.Type);

        for (int row = 0; row < piece.Size; row++)
        {
            for (int col = 0; col < piece.Size; col++)
            {
                if (piece.Cells[row, col] == 0)
                {
                    continue;
                }

                int boardRow = ghostY + row;

                if (boardRow < 0 || boardRow >= TetrisGame.Rows)
                {
                    continue;
                }

                DrawGhostBlock(g, CellRectangle(_game.CurrentX + col, boardRow), color);
            }
        }
    }

    private void DrawCurrentPiece(Graphics g)
    {
        if (_game.State == GameState.GameOver)
        {
            return;
        }

        Tetromino piece = _game.Current;
        Color color = BlockColor(piece.Type);

        for (int row = 0; row < piece.Size; row++)
        {
            for (int col = 0; col < piece.Size; col++)
            {
                if (piece.Cells[row, col] == 0)
                {
                    continue;
                }

                float boardRow = _pieceRenderY + row;

                if (boardRow < 0f || boardRow >= TetrisGame.Rows)
                {
                    continue;
                }

                DrawBlock(g, CellRectangle(_game.CurrentX + col, boardRow), color);
            }
        }
    }

    private void DrawSidePanel(Graphics g)
    {
        DrawNextCard(g);
        DrawScoreCard(g);
        DrawLevelAndLinesCards(g);
        DrawControlsCard(g);
        DrawLeaderboardCard(g);
    }

    private void DrawNextCard(Graphics g)
    {
        var card = new RectangleF(SideX, 88, 220, 148);
        DrawCard(g, card, Color.FromArgb(48, 34, 211, 238));
        DrawCardTitle(g, "下一个", SideX + 16f, 100f, Color.FromArgb(34, 211, 238));

        var preview = new RectangleF(SideX + 14, 122, 192, 104);
        using (var previewPath = RoundedRectangle(preview, 8))
        using (var previewBrush = new SolidBrush(Color.FromArgb(110, 4, 8, 17)))
        using (var previewPen = new Pen(Color.FromArgb(38, 56, 84), 1f))
        {
            g.FillPath(previewBrush, previewPath);
            g.DrawPath(previewPen, previewPath);
        }

        Tetromino piece = Tetromino.Create(_game.NextType);
        GetBlockBounds(piece, out int minRow, out int maxRow, out int minCol, out int maxCol);

        const float previewCell = 24f;
        int width = maxCol - minCol + 1;
        int height = maxRow - minRow + 1;
        float startX = preview.X + (preview.Width - width * previewCell) / 2f;
        float startY = preview.Y + (preview.Height - height * previewCell) / 2f;
        Color color = BlockColor(piece.Type);

        for (int row = minRow; row <= maxRow; row++)
        {
            for (int col = minCol; col <= maxCol; col++)
            {
                if (piece.Cells[row, col] == 0)
                {
                    continue;
                }

                var rect = new RectangleF(
                    startX + (col - minCol) * previewCell + 2,
                    startY + (row - minRow) * previewCell + 2,
                    previewCell - 4,
                    previewCell - 4);
                DrawBlock(g, rect, color, 5f);
            }
        }
    }

    private void DrawScoreCard(Graphics g)
    {
        var card = new RectangleF(SideX, 252, 220, 76);
        DrawCard(g, card, Color.FromArgb(46, 34, 211, 238));
        DrawCardTitle(g, "分数 · 十六进制", SideX + 16f, 264f, Color.FromArgb(103, 232, 249));

        using var rightAligned = new StringFormat { Alignment = StringAlignment.Far };
        g.DrawString(
            $"最高 0x{_highScoreStore.HighScore:X4}",
            _recordFont,
            RecordBrush,
            new RectangleF(SideX + 14f, 264f, 192f, 16f),
            rightAligned);

        // 得分增长时数字轻微弹跳。
        float popScale = 1f;

        if (_scorePopAt >= 0)
        {
            float pop = (_clock.ElapsedMilliseconds - _scorePopAt) / 220f;

            if (pop >= 1f)
            {
                _scorePopAt = -1;
            }
            else
            {
                popScale = 1f + 0.22f * (1f - pop) * (1f - pop);
            }
        }

        string scoreText = $"0x{_game.Score:X4}";

        if (popScale > 1.001f)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(SideX + 14f, 282f);
            g.ScaleTransform(popScale, popScale);
            g.DrawString(scoreText, _scoreFont, ScoreBrush, 0f, 0f);
            g.Restore(state);
        }
        else
        {
            g.DrawString(scoreText, _scoreFont, ScoreBrush, SideX + 14f, 282f);
        }

        // 分数增量飘字：金色 +0xXX 在分数右上方浮起并淡出。
        if (_scorePopAt >= 0 && _scoreDelta > 0)
        {
            float rise = (_clock.ElapsedMilliseconds - _scorePopAt) / 620f;

            if (rise < 1f)
            {
                using var deltaBrush = new SolidBrush(
                    Color.FromArgb(ScaleAlpha(235, 1f - rise), 251, 191, 36));
                g.DrawString(
                    $"+0x{_scoreDelta:X}",
                    _recordFont,
                    deltaBrush,
                    SideX + 148f,
                    296f - 14f * rise);
            }
        }
    }

    private void DrawLevelAndLinesCards(Graphics g)
    {
        var levelCard = new RectangleF(SideX, 340, 104, 76);
        var linesCard = new RectangleF(SideX + 116, 340, 104, 76);

        DrawCard(g, levelCard, Color.FromArgb(44, 167, 139, 250));
        DrawCard(g, linesCard, Color.FromArgb(44, 52, 211, 153));

        // 升级瞬间给等级卡片一圈青色辉光。
        if (_levelUpAt >= 0)
        {
            float glow = (_clock.ElapsedMilliseconds - _levelUpAt) / 650f;

            if (glow >= 1f)
            {
                _levelUpAt = -1;
            }
            else
            {
                using var glowPath = RoundedRectangle(levelCard, 10);
                using var glowPen = new Pen(Color.FromArgb(ScaleAlpha(190, 1f - glow), 34, 211, 238), 2f);
                g.DrawPath(glowPen, glowPath);
            }
        }

        DrawCardTitle(g, "等级", levelCard.X + 14f, levelCard.Y + 12f, Color.FromArgb(167, 139, 250));
        g.DrawString(_game.Level.ToString("D2"), _statFont, ValueBrush, levelCard.X + 12f, levelCard.Y + 30f);

        DrawCardTitle(g, "消行", linesCard.X + 14f, linesCard.Y + 12f, Color.FromArgb(52, 211, 153));
        g.DrawString(_game.Lines.ToString("D2"), _statFont, ValueBrush, linesCard.X + 12f, linesCard.Y + 30f);
    }

    private void DrawControlsCard(Graphics g)
    {
        var card = new RectangleF(SideX, 428, 220, 172);
        DrawCard(g, card, Color.FromArgb(44, 125, 211, 252));
        DrawCardTitle(g, "操作", SideX + 16f, 441f, Color.FromArgb(125, 211, 252));

        (string Key, string Action)[] rows =
        {
            ("← →", "移动"),
            ("↑ / Z", "旋转"),
            ("↓", "软降"),
            ("SPACE", "瞬间下落"),
            ("P", "暂停 / 继续"),
            ("R", "重新开始")
        };

        float y = 462f;

        foreach ((string key, string action) in rows)
        {
            g.DrawString(key, _keyFont, KeyBrush, SideX + 16f, y);
            g.DrawString(action, _labelFont, DescBrush, SideX + 82f, y);
            y += 24f;
        }
    }

    private void DrawLeaderboardCard(Graphics g)
    {
        var card = new RectangleF(SideX, 604, 220, 88);
        DrawCard(g, card, Color.FromArgb(44, 251, 191, 36));
        DrawCardTitle(g, "排行榜", SideX + 16f, 616f, Color.FromArgb(251, 191, 36));

        float y = 634f;

        if (_topScores is null || _topScores.Status == ScoreboardStatus.IoError)
        {
            g.DrawString("排行榜不可用", _labelFont, MutedBrush, SideX + 16f, y);
        }
        else if (_topScores.Status == ScoreboardStatus.Empty)
        {
            g.DrawString("暂无记录", _labelFont, MutedBrush, SideX + 16f, y);
        }
        else if (_topScores.Status == ScoreboardStatus.Corrupted)
        {
            g.DrawString("数据库损坏", _labelFont, RecordBrush, SideX + 16f, y);
        }
        else
        {
            using var rightAligned = new StringFormat { Alignment = StringAlignment.Far };
            int count = Math.Min(3, _topScores.Entries.Count);

            for (int i = 0; i < count; i++)
            {
                ScoreEntry entry = _topScores.Entries[i];
                string rank = (i + 1).ToString();
                string name = entry.PlayerName.Length > 8
                    ? entry.PlayerName.AsSpan(0, 7).ToString() + "…"
                    : entry.PlayerName;
                string score = $"0x{entry.Score:X4}";

                Brush rankBrush = i switch
                {
                    0 => RecordBrush,
                    1 => ValueBrush,
                    2 => new SolidBrush(Color.FromArgb(234, 179, 8)),
                    _ => MutedBrush
                };

                g.DrawString($"{rank}.", _keyFont, rankBrush, SideX + 16f, y);
                g.DrawString(name, _labelFont, DescBrush, SideX + 40f, y);
                g.DrawString(score, _keyFont, KeyBrush,
                    new RectangleF(SideX + 140f, y - 2f, 64f, 18f),
                    rightAligned);

                y += 16f;
            }
        }

        // "查看全部"按钮 — 标题行右侧，右对齐避开分数列，只缩小字号。
        _showHistoryButtonRect = new RectangleF(SideX + 162f, 618f, 46f, 12f);
        using var btnBrush = new SolidBrush(Color.FromArgb(251, 191, 36));
        using var btnFont = new Font("Segoe UI", 5.5f, FontStyle.Bold);
        using var btnFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("查看全部", btnFont, btnBrush, _showHistoryButtonRect, btnFormat);
    }

    private RectangleF _showHistoryButtonRect;

    private void DrawFooter(Graphics g)
    {
        // 与页头呼应的分隔线，收拢底部区域（只覆盖棋盘列，右侧留给排行榜卡片）。
        using var dividerPen = new Pen(Color.FromArgb(28, 54, 82), 1f);
        g.DrawLine(dividerPen, 28f, 612f, 430f, 612f);

        if (_notice is not null && _clock.ElapsedMilliseconds < _noticeUntil)
        {
            using var noticeBrush = new SolidBrush(Color.FromArgb(251, 191, 36));
            g.DrawString(_notice, _labelFont, noticeBrush, 28f, 622f);
        }
        else
        {
            g.DrawString("计分规则   100 × 消除行数", _labelFont, MutedBrush, 28f, 622f);
        }

        using var rightAligned = new StringFormat { Alignment = StringAlignment.Far };
        g.DrawString(
            "P 暂停  ·  R 重开",
            _labelFont,
            MutedBrush,
            new RectangleF(266, 622, 164, 18),
            rightAligned);
    }

    private void DrawStartOverlay(Graphics g, float alphaScale = 1f)
    {
        using var scoreBrush = Scaled(ScoreBrush, alphaScale);
        using var valueBrush = Scaled(ValueBrush, alphaScale);
        using var descBrush = Scaled(DescBrush, alphaScale);
        using var keyBrush = Scaled(KeyBrush, alphaScale);
        using var recordBrush = Scaled(RecordBrush, alphaScale);

        using var overlayPath = RoundedRectangle(BoardRect, 12);
        using var overlayBrush = new SolidBrush(Color.FromArgb(ScaleAlpha(210, alphaScale), 5, 8, 18));
        g.FillPath(overlayBrush, overlayPath);

        // 顶部装饰光带
        var glowRect = new RectangleF(BoardRect.X + BoardRect.Width / 2f - 80f, BoardRect.Y + 70f, 160f, 4f);
        using var glowPath = RoundedRectangle(glowRect, 2f);
        using var glowBrush = new LinearGradientBrush(
            glowRect,
            Color.FromArgb(ScaleAlpha(255, alphaScale), 34, 211, 238),
            Color.FromArgb(ScaleAlpha(255, alphaScale), 167, 139, 250),
            0f);
        g.FillPath(glowBrush, glowPath);

        DrawCenteredText(g, "准备开始", _overlayTitleFont, scoreBrush, BoardRect, 92f);
        DrawCenteredText(g, "俄罗斯方块 · 12 × 16", _overlayBodyFont, descBrush, BoardRect, 146f);

        // 最高分展示
        string highScore = $"最高纪录  0x{_highScoreStore.HighScore:X4}";
        DrawCenteredText(g, highScore, _recordFont, recordBrush, BoardRect, 186f);

        // 操作提示
        float startY = BoardRect.Y + 230f;
        (string Key, string Action)[] hints =
        {
            ("← →", "左右移动"),
            ("↑ / Z", "顺 / 逆旋转"),
            ("↓", "软降"),
            ("SPACE", "瞬间下落")
        };

        foreach ((string key, string action) in hints)
        {
            g.DrawString(key, _keyFont, keyBrush, BoardRect.X + 86f, startY);
            g.DrawString(action, _labelFont, descBrush, BoardRect.X + 168f, startY);
            startY += 26f;
        }

        // 闪烁的开始提示
        long blink = _clock.ElapsedMilliseconds % 1200;
        if (blink < 700)
        {
            DrawCenteredText(g, "按任意键开始", _overlayBodyFont, valueBrush, BoardRect, 372f);
        }
    }

    private void DrawPauseOverlay(Graphics g)
    {
        using var overlayPath = RoundedRectangle(BoardRect, 12);
        using var overlayBrush = new SolidBrush(Color.FromArgb(205, 4, 7, 14));
        g.FillPath(overlayBrush, overlayPath);

        DrawCenteredText(g, "已暂停", _overlayTitleFont, ScoreBrush, BoardRect, 190f);
        DrawCenteredText(g, "按 P 继续游戏", _overlayBodyFont, DescBrush, BoardRect, 244f);
    }

    private void DrawGameOverOverlay(Graphics g)
    {
        // 结束界面淡入，标题与分数轻微下落，避免生硬弹出。
        float t = _gameOverSince < 0
            ? 1f
            : Math.Min(1f, (_clock.ElapsedMilliseconds - _gameOverSince) / 480f);
        float ease = 1f - (1f - t) * (1f - t);
        float slide = (1f - ease) * -12f;

        using var overlayPath = RoundedRectangle(BoardRect, 12);
        using var overlayBrush = new SolidBrush(Color.FromArgb(ScaleAlpha(226, ease), 4, 7, 14));
        g.FillPath(overlayBrush, overlayPath);

        using var accentPen = new Pen(Color.FromArgb(ScaleAlpha(180, ease), 248, 113, 113), 2f);
        float centerX = BoardRect.X + BoardRect.Width / 2f;
        float accentY = BoardRect.Y + 142f + slide;
        g.DrawLine(accentPen, centerX - 24f, accentY, centerX + 24f, accentY);

        using var dangerBrush = new SolidBrush(Color.FromArgb(ScaleAlpha(255, ease), 248, 113, 113));
        DrawCenteredText(g, "游戏结束", _overlayTitleFont, dangerBrush, BoardRect, 166f + slide);

        using var valueBrush = Scaled(ValueBrush, ease);
        DrawCenteredText(
            g,
            $"分数 0x{_game.Score:X4}  ·  {_game.Lines} 行",
            _overlayBodyFont,
            valueBrush,
            BoardRect,
            220f + slide * 0.5f);

        using var descBrush = Scaled(DescBrush, ease);
        DrawCenteredText(g, "按 R 重新开始", _overlayBodyFont, descBrush, BoardRect, 258f);
    }

    private static void DrawCenteredText(
        Graphics g,
        string text,
        Font font,
        Brush brush,
        Rectangle board,
        float y)
    {
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        g.DrawString(text, font, brush, new RectangleF(board.X, y, board.Width, 38f), format);
    }

    private static void DrawCard(Graphics g, RectangleF rect, Color? accentBorder = null)
    {
        using var path = RoundedRectangle(rect, 10);
        using var fill = new LinearGradientBrush(
            rect,
            Color.FromArgb(19, 26, 47),
            Color.FromArgb(12, 17, 33),
            90f);
        g.FillPath(fill, path);

        using var border = new Pen(accentBorder ?? Color.FromArgb(36, 48, 78), 1f);
        g.DrawPath(border, path);

        // 顶部内侧高光线：给卡片一点轻微的立体感。
        using var highlight = new Pen(Color.FromArgb(24, 255, 255, 255), 1f);
        g.DrawLine(highlight, rect.X + 11f, rect.Y + 1.5f, rect.Right - 11f, rect.Y + 1.5f);
    }

    /// <summary>卡片标题：左侧一枚圆角色块作图标感标记，右侧跟标签文字。</summary>
    private void DrawCardTitle(Graphics g, string text, float x, float y, Color accent)
    {
        using var markerPath = RoundedRectangle(new RectangleF(x, y + 3f, 4f, 13f), 2f);
        using var markerBrush = new SolidBrush(accent);
        g.FillPath(markerBrush, markerPath);

        g.DrawString(text, _labelFont, LabelBrush, x + 11f, y);
    }

    private static void DrawBlock(Graphics g, RectangleF rect, Color color, float radius = 6f)
    {
        using var path = RoundedRectangle(rect, radius);
        using var fill = new LinearGradientBrush(
            rect,
            ControlPaint.Light(color, 0.42f),
            ControlPaint.Dark(color, 0.1f),
            90f);
        g.FillPath(fill, path);

        using var border = new Pen(Color.FromArgb(135, ControlPaint.Light(color, 0.65f)), 1f);
        g.DrawPath(border, path);

        var glossRect = new RectangleF(
            rect.X + 2f,
            rect.Y + 2f,
            Math.Max(1f, rect.Width - 4f),
            Math.Max(1f, rect.Height * 0.3f));
        using var glossPath = RoundedRectangle(glossRect, Math.Min(3f, glossRect.Height / 2f));
        using var glossBrush = new LinearGradientBrush(
            glossRect,
            Color.FromArgb(64, Color.White),
            Color.FromArgb(0, Color.White),
            90f);
        g.FillPath(glossBrush, glossPath);
    }

    private static void DrawGhostBlock(Graphics g, RectangleF rect, Color color)
    {
        using var path = RoundedRectangle(rect, 5f);
        using var fill = new SolidBrush(Color.FromArgb(18, color));
        g.FillPath(fill, path);

        using var pen = new Pen(Color.FromArgb(105, color), 1.2f)
        {
            DashStyle = DashStyle.Dash
        };
        g.DrawPath(pen, path);
    }

    private static RectangleF CellRectangle(int col, float row)
    {
        return new RectangleF(
            BoardX + col * CellSize + 2.5f,
            BoardY + row * CellSize + 2.5f,
            CellSize - 5f,
            CellSize - 5f);
    }

    private static void GetBlockBounds(
        Tetromino piece,
        out int minRow,
        out int maxRow,
        out int minCol,
        out int maxCol)
    {
        minRow = piece.Size;
        maxRow = -1;
        minCol = piece.Size;
        maxCol = -1;

        for (int row = 0; row < piece.Size; row++)
        {
            for (int col = 0; col < piece.Size; col++)
            {
                if (piece.Cells[row, col] == 0)
                {
                    continue;
                }

                minRow = Math.Min(minRow, row);
                maxRow = Math.Max(maxRow, row);
                minCol = Math.Min(minCol, col);
                maxCol = Math.Max(maxCol, col);
            }
        }
    }

    private static Color BlockColor(TetrominoType type)
    {
        return type switch
        {
            TetrominoType.I => Color.FromArgb(34, 211, 238),
            TetrominoType.J => Color.FromArgb(59, 130, 246),
            TetrominoType.L => Color.FromArgb(251, 146, 60),
            TetrominoType.O => Color.FromArgb(250, 204, 21),
            TetrominoType.S => Color.FromArgb(52, 211, 153),
            TetrominoType.T => Color.FromArgb(167, 139, 250),
            TetrominoType.Z => Color.FromArgb(248, 113, 113),
            _ => Color.White
        };
    }

    private static Font PixelFont(string family, float pixelSize, FontStyle style = FontStyle.Regular)
    {
        return new Font(family, pixelSize, style, GraphicsUnit.Pixel);
    }

    private static int ScaleAlpha(int alpha, float scale)
    {
        return (int)Math.Clamp(alpha * scale, 0f, 255f);
    }

    private static SolidBrush Scaled(SolidBrush brush, float scale)
    {
        return new SolidBrush(Color.FromArgb(ScaleAlpha(brush.Color.A, scale), brush.Color));
    }

    private static Color Mix(Color from, Color to, float t)
    {
        int r = (int)(from.R + (to.R - from.R) * t);
        int g = (int)(from.G + (to.G - from.G) * t);
        int b = (int)(from.B + (to.B - from.B) * t);
        return Color.FromArgb(r, g, b);
    }

    private sealed class LockFlash
    {
        public required int X { get; init; }
        public required int Y { get; init; }
        public required Color Color { get; init; }
        public required long Start { get; init; }
    }

    private sealed class ClearFlash
    {
        public required int Row { get; init; }
        public required TetrominoType[] Cells { get; init; }
        public required long Start { get; init; }
    }

    private static GraphicsPath RoundedRectangle(RectangleF rect, float radius)
    {
        float diameter = radius * 2f;
        var path = new GraphicsPath();

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180f, 90f);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270f, 90f);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();

        return path;
    }
}

using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Tetris.Game;

namespace Tetris.UI;

public sealed class MainForm : Form
{
    private const int DesignWidth = 700;
    private const int DesignHeight = 660;
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

    private readonly TetrisGame _game = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly HashSet<Keys> _pressed = new();

    private readonly Font _titleFont = PixelFont("Segoe UI", 40f, FontStyle.Bold);
    private readonly Font _subtitleFont = PixelFont("Segoe UI", 12f);
    private readonly Font _labelFont = PixelFont("Segoe UI", 12f);
    private readonly Font _scoreFont = PixelFont("Consolas", 32f, FontStyle.Bold);
    private readonly Font _statFont = PixelFont("Consolas", 29f, FontStyle.Bold);
    private readonly Font _keyFont = PixelFont("Consolas", 12f, FontStyle.Bold);
    private readonly Font _overlayTitleFont = PixelFont("Segoe UI", 33f, FontStyle.Bold);
    private readonly Font _overlayBodyFont = PixelFont("Segoe UI", 14f);

    private float _scale = 1f;
    private long _lastTick;
    private long _nextLeftRepeat;
    private long _nextRightRepeat;
    private long _nextDownRepeat;

    public MainForm()
    {
        Text = "Tetris · 俄罗斯方块";
        ClientSize = new Size(700, 660);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
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

        _timer = new System.Windows.Forms.Timer { Interval = 16 };
        _timer.Tick += OnGameTick;
        _lastTick = _clock.ElapsedMilliseconds;
        _timer.Start();
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
            _overlayTitleFont.Dispose();
            _overlayBodyFont.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        _scale = DeviceDpi / 96f;
        ClientSize = new Size(
            (int)Math.Round(DesignWidth * _scale),
            (int)Math.Round(DesignHeight * _scale));
        CenterToScreen();
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        _pressed.Clear();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

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

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.ScaleTransform(_scale, _scale);

        DrawBackground(g);
        DrawHeader(g);
        DrawBoard(g);
        DrawSidePanel(g);
        DrawFooter(g);
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

        DrawGrid(g);
        DrawLockedBlocks(g);
        DrawGhostPiece(g);
        DrawCurrentPiece(g);

        g.Restore(state);

        using var borderPen = new Pen(Color.FromArgb(45, 62, 96), 1.4f);
        g.DrawPath(borderPen, boardPath);

        if (_game.State == GameState.Paused)
        {
            DrawPauseOverlay(g);
        }
        else if (_game.State == GameState.GameOver)
        {
            DrawGameOverOverlay(g);
        }
    }

    private static void DrawGrid(Graphics g)
    {
        using var gridPen = new Pen(Color.FromArgb(22, 116, 140, 180), 1f);

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

                int boardRow = _game.CurrentY + row;

                if (boardRow < 0 || boardRow >= TetrisGame.Rows)
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
    }

    private void DrawNextCard(Graphics g)
    {
        var card = new RectangleF(SideX, 88, 220, 148);
        DrawCard(g, card, Color.FromArgb(48, 34, 211, 238));
        g.DrawString("下一个", _labelFont, LabelBrush, SideX + 16f, 100f);

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
        g.DrawString("分数 · 十六进制", _labelFont, LabelBrush, SideX + 16f, 264f);
        g.DrawString($"0x{_game.Score:X4}", _scoreFont, ScoreBrush, SideX + 14f, 282f);
    }

    private void DrawLevelAndLinesCards(Graphics g)
    {
        var levelCard = new RectangleF(SideX, 340, 104, 76);
        var linesCard = new RectangleF(SideX + 116, 340, 104, 76);

        DrawCard(g, levelCard);
        DrawCard(g, linesCard);

        g.DrawString("等级", _labelFont, LabelBrush, levelCard.X + 14f, levelCard.Y + 12f);
        g.DrawString(_game.Level.ToString("D2"), _statFont, ValueBrush, levelCard.X + 12f, levelCard.Y + 30f);

        g.DrawString("消行", _labelFont, LabelBrush, linesCard.X + 14f, linesCard.Y + 12f);
        g.DrawString(_game.Lines.ToString("D2"), _statFont, ValueBrush, linesCard.X + 12f, linesCard.Y + 30f);
    }

    private void DrawControlsCard(Graphics g)
    {
        var card = new RectangleF(SideX, 428, 220, 172);
        DrawCard(g, card);
        g.DrawString("操作", _labelFont, LabelBrush, SideX + 16f, 441f);

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

    private void DrawFooter(Graphics g)
    {
        g.DrawString("计分规则   100 × 消除行数", _labelFont, MutedBrush, 28f, 622f);

        using var rightAligned = new StringFormat { Alignment = StringAlignment.Far };
        g.DrawString(
            "P 暂停  ·  R 重开",
            _labelFont,
            MutedBrush,
            new RectangleF(500, 622, 164, 18),
            rightAligned);
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
        using var overlayPath = RoundedRectangle(BoardRect, 12);
        using var overlayBrush = new SolidBrush(Color.FromArgb(226, 4, 7, 14));
        g.FillPath(overlayBrush, overlayPath);

        using var accentPen = new Pen(Color.FromArgb(180, 248, 113, 113), 2f);
        float centerX = BoardRect.X + BoardRect.Width / 2f;
        g.DrawLine(accentPen, centerX - 24f, BoardRect.Y + 142f, centerX + 24f, BoardRect.Y + 142f);

        using var dangerBrush = new SolidBrush(Color.FromArgb(248, 113, 113));
        DrawCenteredText(g, "游戏结束", _overlayTitleFont, dangerBrush, BoardRect, 166f);
        DrawCenteredText(
            g,
            $"分数 0x{_game.Score:X4}  ·  {_game.Lines} 行",
            _overlayBodyFont,
            ValueBrush,
            BoardRect,
            220f);
        DrawCenteredText(g, "按 R 重新开始", _overlayBodyFont, DescBrush, BoardRect, 258f);
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

    private static RectangleF CellRectangle(int col, int row)
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

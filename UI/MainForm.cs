using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Tetris.Game;
using Tetris.Persistence;

namespace Tetris.UI;

public sealed partial class MainForm : Form
{
    private const int InitialClientWidth = 720;
    private const int InitialClientHeight = 720;
    private const int MinimumClientWidth = 480;
    private const int MinimumClientHeight = 560;
    private const int ThemeTransitionMs = 320;
    private const int CardRevealMs = 360;

    private readonly TetrisGame _game = new();
    private readonly IHighScoreStore _highScoreStore = new HighScoreStore();
    private readonly IScoreRepository _scoreRepository = new SqliteScoreRepository();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly HashSet<Keys> _pressed = new();

    private ScoreboardResult? _topScores;
    private long _scoresLoadedAt = -1;

    private readonly Font _displayFont = PixelFont("Segoe UI", 34f, FontStyle.Bold);
    private readonly Font _subtitleFont = PixelFont("Segoe UI", 11f);
    private readonly Font _cardTitleFont = PixelFont("Segoe UI", 11f, FontStyle.Bold);
    private readonly Font _labelFont = PixelFont("Segoe UI", 11f);
    private readonly Font _captionFont = PixelFont("Segoe UI", 10f);
    private readonly Font _captionBoldFont = PixelFont("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _scoreFont = PixelFont("Consolas", 28f, FontStyle.Bold);
    private readonly Font _statFont = PixelFont("Consolas", 24f, FontStyle.Bold);
    private readonly Font _keyFont = PixelFont("Consolas", 10f, FontStyle.Bold);
    private readonly Font _buttonFont = PixelFont("Segoe UI", 10f, FontStyle.Bold);
    private readonly Font _overlayTitleFont = PixelFont("Segoe UI", 26f, FontStyle.Bold);
    private readonly Font _overlayBodyFont = PixelFont("Segoe UI", 12f);
    private readonly Font _rankFont = PixelFont("Consolas", 10.5f, FontStyle.Bold);

    private ResponsiveLayout _layout = ResponsiveLayout.Create(InitialClientWidth, InitialClientHeight);
    private UiTheme _themeFrom;
    private UiTheme _themeTo;
    private long _themeStart = -10_000;
    private long _nextThemeCheck;
    private bool _isLightTheme;
    private long _shownAt;

    private long _lastTick;
    private long _nextLeftRepeat;
    private long _nextRightRepeat;
    private long _nextDownRepeat;
    private GameState _lastState = GameState.Ready;
    private string? _notice;
    private long _noticeUntil;

    private readonly List<LockFlash> _lockFlashes = new();
    private readonly List<ClearFlash> _clearFlashes = new();
    private Tetromino? _renderPiece;
    private float _pieceRenderY;
    private float _pieceTweenFrom;
    private float _pieceTweenTo;
    private float _pieceTweenTarget;
    private long _pieceTweenStart;
    private bool _pieceTweenActive;
    private long _gameOverSince = -1;
    private long _pauseSince = -1;
    private long _startedAt = -1;
    private int _lastScore;
    private int _lastLevel = 1;
    private int _scoreDelta;
    private long _scorePopAt = -1;
    private long _levelUpAt = -1;

    private bool _historyHovered;
    private bool _historyPressed;
    private float _historyHoverProgress;

    public MainForm()
    {
        _isLightTheme = SystemTheme.IsLightMode();
        _themeTo = _isLightTheme ? UiTheme.Light : UiTheme.Dark;
        _themeFrom = _themeTo;

        Text = "Tetris · 俄罗斯方块";
        ClientSize = new Size(InitialClientWidth, InitialClientHeight);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = _themeTo.CanvasBottom;
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

        try
        {
            _topScores = _scoreRepository.GetTopScores(10);
            _scoresLoadedAt = 0;
        }
        catch (ScoreRepositoryException)
        {
            _topScores = new ScoreboardResult(
                ScoreboardStatus.IoError,
                Array.Empty<ScoreEntry>(),
                "排行榜不可用");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _displayFont.Dispose();
            _subtitleFont.Dispose();
            _cardTitleFont.Dispose();
            _labelFont.Dispose();
            _captionFont.Dispose();
            _captionBoldFont.Dispose();
            _scoreFont.Dispose();
            _statFont.Dispose();
            _keyFont.Dispose();
            _buttonFont.Dispose();
            _overlayTitleFont.Dispose();
            _overlayBodyFont.Dispose();
            _rankFont.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyWindowTheme(_isLightTheme);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        float scale = DeviceDpi / 96f;
        Rectangle working = Screen.FromControl(this).WorkingArea;

        int nonClientWidth = Width - ClientSize.Width;
        int nonClientHeight = Height - ClientSize.Height;
        MinimumSize = new Size(
            (int)Math.Round(MinimumClientWidth * scale) + nonClientWidth,
            (int)Math.Round(MinimumClientHeight * scale) + nonClientHeight);

        int initialWidth = Math.Min((int)Math.Round(InitialClientWidth * scale), working.Width - 80);
        int initialHeight = Math.Min((int)Math.Round(InitialClientHeight * scale), working.Height - 80);

        ClientSize = new Size(
            Math.Max(initialWidth, MinimumClientWidth),
            Math.Max(initialHeight, MinimumClientHeight));
        CenterToScreen();

        _shownAt = _clock.ElapsedMilliseconds;
        _nextThemeCheck = _shownAt + 1000;
        ApplyWindowTheme(_isLightTheme);
    }

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        _pressed.Clear();
        ResetHistoryInteraction();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        SubmitScore();
        base.OnFormClosing(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ResetHistoryInteraction();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        PointF point = ToLogicalPoint(e.Location);
        bool hit = HistoryButtonEnabled && UpdateLayout().HistoryButton.Contains(point);

        if (hit != _historyHovered)
        {
            _historyHovered = hit;
            Cursor = hit ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        PointF point = ToLogicalPoint(e.Location);
        _historyPressed = HistoryButtonEnabled &&
                          UpdateLayout().HistoryButton.Contains(point);

        if (_historyPressed)
        {
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        PointF point = ToLogicalPoint(e.Location);
        bool shouldOpen = HistoryButtonEnabled &&
                          _historyPressed &&
                          UpdateLayout().HistoryButton.Contains(point);
        _historyPressed = false;

        if (shouldOpen)
        {
            ShowHistoryDialog();
        }

        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        ResetHistoryInteraction();
    }

    private void ResetHistoryInteraction()
    {
        _historyHovered = false;
        _historyPressed = false;
        Cursor = Cursors.Default;
        Invalidate();
    }

    private PointF ToLogicalPoint(Point point)
    {
        float scale = DeviceDpi / 96f;
        return new PointF(point.X / scale, point.Y / scale);
    }

    private ResponsiveLayout UpdateLayout()
    {
        float scale = DeviceDpi / 96f;
        _layout = ResponsiveLayout.Create(
            Math.Max(ClientSize.Width, 1) / scale,
            Math.Max(ClientSize.Height, 1) / scale);
        return _layout;
    }

    private bool HistoryButtonEnabled =>
        _topScores is not null &&
        _topScores.Status is not (ScoreboardStatus.IoError or ScoreboardStatus.Corrupted);

    private UiTheme CurrentTheme
    {
        get
        {
            long now = _clock.ElapsedMilliseconds;
            float t = Math.Clamp((now - _themeStart) / (float)ThemeTransitionMs, 0f, 1f);

            if (t >= 1f)
            {
                return _themeTo;
            }

            t = t * t * (3f - 2f * t);
            return UiTheme.Lerp(_themeFrom, _themeTo, t);
        }
    }

    private void CheckSystemTheme(long now)
    {
        if (now < _nextThemeCheck)
        {
            return;
        }

        _nextThemeCheck = now + 1000;
        bool light = SystemTheme.IsLightMode();

        if (light == _isLightTheme)
        {
            return;
        }

        _isLightTheme = light;
        _themeFrom = CurrentTheme;
        _themeTo = light ? UiTheme.Light : UiTheme.Dark;
        _themeStart = now;
        ApplyWindowTheme(light);
        Invalidate();
    }

    private void ApplyWindowTheme(bool light)
    {
        BackColor = light ? UiTheme.Light.CanvasBottom : UiTheme.Dark.CanvasBottom;

        if (IsHandleCreated)
        {
            ApplyDarkTitleBar(Handle, !light);
        }
    }

    private static void ApplyDarkTitleBar(IntPtr handle, bool dark)
    {
        try
        {
            int value = dark ? 1 : 0;
            DwmSetWindowAttribute(handle, 20, ref value, sizeof(int));
        }
        catch
        {
            // 旧版 Windows 或受限环境忽略标题栏主题设置。
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

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
                _nextDownRepeat = now + 70;
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

        CheckSystemTheme(now);
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
                _nextDownRepeat = now + 30;
            }
        }

        if (_game.State == GameState.GameOver && _lastState != GameState.GameOver)
        {
            SubmitScore();
        }

        UpdateAnimations(now, elapsed);

        float hoverTarget = _historyHovered ? 1f : 0f;
        float hoverFactor = 1f - MathF.Exp(-elapsed / 70f);
        _historyHoverProgress += (hoverTarget - _historyHoverProgress) * hoverFactor;

        _lastState = _game.State;
        Invalidate();
    }

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

        if (_game.State == GameState.Paused && _lastState != GameState.Paused)
        {
            _pauseSince = now;
        }
        else if (_game.State != GameState.Paused)
        {
            _pauseSince = -1;
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

        if (!ReferenceEquals(_renderPiece, _game.Current) || _game.State != GameState.Running)
        {
            _renderPiece = _game.Current;
            _pieceRenderY = _game.CurrentY;
            _pieceTweenFrom = _game.CurrentY;
            _pieceTweenTo = _game.CurrentY;
            _pieceTweenTarget = _game.CurrentY;
            _pieceTweenActive = false;
        }
        else
        {
            float targetY = _game.CurrentY;

            if (Math.Abs(targetY - _pieceTweenTarget) > 0.001f)
            {
                float distance = targetY - _pieceRenderY;

                if (distance < 0f || distance > 3f)
                {
                    _pieceRenderY = targetY;
                    _pieceTweenActive = false;
                }
                else
                {
                    _pieceTweenFrom = _pieceRenderY;
                    _pieceTweenTo = targetY;
                    _pieceTweenStart = now;
                    _pieceTweenActive = true;
                }

                _pieceTweenTarget = targetY;
            }

            if (_pieceTweenActive)
            {
                float duration = Math.Clamp(38f * MathF.Abs(_pieceTweenTo - _pieceTweenFrom), 32f, 96f);
                float progress = (now - _pieceTweenStart) / duration;

                if (progress >= 1f)
                {
                    _pieceRenderY = _pieceTweenTo;
                    _pieceTweenActive = false;
                }
                else
                {
                    _pieceRenderY = _pieceTweenFrom +
                                    (_pieceTweenTo - _pieceTweenFrom) * EaseOutCubic(progress);
                }
            }
        }
    }

    private void OnPieceLocked(IReadOnlyList<(int X, int Y)> cells, TetrominoType type)
    {
        long now = _clock.ElapsedMilliseconds;

        foreach ((int x, int y) in cells)
        {
            _lockFlashes.Add(new LockFlash
            {
                X = x,
                Y = y,
                Type = type,
                Start = now
            });
        }
    }

    private void OnLinesCleared(IReadOnlyList<ClearedRow> rows)
    {
        long now = _clock.ElapsedMilliseconds;
        var clearedRows = new HashSet<int>();

        foreach (ClearedRow row in rows)
        {
            clearedRows.Add(row.Row);
            _clearFlashes.Add(new ClearFlash
            {
                Row = row.Row,
                Cells = row.Cells,
                Start = now
            });
        }

        _lockFlashes.RemoveAll(flash => clearedRows.Contains(flash.Y));
    }

    private void SubmitScore()
    {
        _highScoreStore.Submit(_game.Score);

        if (_highScoreStore.LastOutcome is SaveOutcome.AccessDenied or SaveOutcome.IoError)
        {
            ShowNotice(_highScoreStore.LastOutcome == SaveOutcome.AccessDenied
                ? "成绩未保存（目录不可写）"
                : "成绩未保存（磁盘写入失败）");
        }

        if (_game.Score <= 0 && _game.Lines <= 0)
        {
            return;
        }

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

    private void ShowNotice(string message)
    {
        _notice = message;
        _noticeUntil = _clock.ElapsedMilliseconds + 6000;
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

        UiTheme theme = CurrentTheme;
        float dpiScale = DeviceDpi / 96f;
        int S(float value) => (int)Math.Round(value * dpiScale);

        using var dialog = new Form
        {
            Text = "历史全部成绩",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            AutoScaleMode = AutoScaleMode.None,
            BackColor = theme.CanvasBottom,
            ForeColor = theme.TextPrimary,
            ClientSize = new Size(S(600), S(540))
        };

        dialog.HandleCreated += (_, _) => ApplyDarkTitleBar(dialog.Handle, theme.IsDark);

        using var dialogTitleFont = new Font("Segoe UI", 15f, FontStyle.Bold);
        using var dialogSubtitleFont = new Font("Segoe UI", 9.5f);
        using var listNameFont = new Font("Segoe UI", 10f, FontStyle.Bold);
        using var listMetaFont = new Font("Segoe UI", 8.5f);
        using var listScoreFont = new Font("Consolas", 11f, FontStyle.Bold);
        using var listRankFont = new Font("Consolas", 9.5f, FontStyle.Bold);
        using var buttonFont = new Font("Segoe UI", 9.5f, FontStyle.Bold);

        var heading = new Label
        {
            Text = "全部历史成绩",
            ForeColor = theme.TextPrimary,
            Font = dialogTitleFont,
            AutoSize = true,
            Location = new Point(S(24), S(20))
        };
        dialog.Controls.Add(heading);

        var subtitle = new Label
        {
            Text = $"按分数降序 · 共 {(result.Status == ScoreboardStatus.Ok ? result.Entries.Count : 0)} 条",
            ForeColor = theme.TextMuted,
            Font = dialogSubtitleFont,
            AutoSize = true,
            Location = new Point(S(26), S(56))
        };
        dialog.Controls.Add(subtitle);

        var list = new ListBox
        {
            Location = new Point(S(24), S(88)),
            Size = new Size(S(552), S(380)),
            Font = listMetaFont,
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = S(52),
            BackColor = theme.CanvasTop,
            ForeColor = theme.TextPrimary,
            ScrollAlwaysVisible = false
        };

        if (result.Status == ScoreboardStatus.Ok && result.Entries.Count > 0)
        {
            foreach (ScoreEntry entry in result.Entries)
            {
                list.Items.Add(entry);
            }
        }
        else
        {
            list.Items.Add(result.Status switch
            {
                ScoreboardStatus.Empty => "暂无历史成绩",
                ScoreboardStatus.Corrupted => "排行榜数据损坏",
                ScoreboardStatus.AccessDenied => "排行榜暂时不可读",
                ScoreboardStatus.IoError => "排行榜读取失败",
                _ => "暂无历史成绩"
            });
        }

        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0)
            {
                return;
            }

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            Rectangle row = e.Bounds;
            bool selected = (e.State & DrawItemState.Selected) != 0;

            Color background = selected
                ? UiTheme.WithAlpha(theme.Accent, theme.IsDark ? 42 : 28)
                : e.Index % 2 == 0 ? theme.CanvasTop : theme.SurfaceMuted;
            using (var backgroundBrush = new SolidBrush(background))
            {
                g.FillRectangle(backgroundBrush, row);
            }

            if (list.Items[e.Index] is not ScoreEntry entry)
            {
                using var emptyFormat = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                using var emptyBrush = new SolidBrush(theme.TextMuted);
                g.DrawString(list.Items[e.Index].ToString(), listNameFont, emptyBrush, row, emptyFormat);
                return;
            }

            Color rankColor = e.Index switch
            {
                0 => theme.Warning,
                1 => theme.TextSecondary,
                2 => theme.Accent,
                _ => theme.TextMuted
            };

            var rankRect = new RectangleF(row.X + S(12), row.Y + S(14), S(24), S(24));
            using (var rankPath = RoundedRectangle(rankRect, 6f * dpiScale))
            using (var rankFill = new SolidBrush(UiTheme.WithAlpha(rankColor, theme.IsDark ? 42 : 26)))
            using (var rankPen = new Pen(UiTheme.WithAlpha(rankColor, theme.IsDark ? 110 : 80), 1f))
            {
                g.FillPath(rankFill, rankPath);
                g.DrawPath(rankPen, rankPath);
            }

            using var rankTextBrush = new SolidBrush(rankColor);
            using var rankFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            g.DrawString((e.Index + 1).ToString(), listRankFont, rankTextBrush, rankRect, rankFormat);

            using var nameBrush = new SolidBrush(theme.TextPrimary);
            using var metaBrush = new SolidBrush(theme.TextMuted);
            using var scoreBrush = new SolidBrush(theme.Accent);
            using var rightFormat = new StringFormat { Alignment = StringAlignment.Far };

            float textX = row.X + S(48);
            float textWidth = row.Width - S(220);
            g.DrawString(
                entry.PlayerName,
                listNameFont,
                nameBrush,
                new RectangleF(textX, row.Y + S(8), textWidth, S(20)));
            g.DrawString(
                $"消行 {entry.Lines}   ·   等级 {entry.Level}   ·   {entry.AchievedAt:yyyy-MM-dd HH:mm}",
                listMetaFont,
                metaBrush,
                new RectangleF(textX, row.Y + S(28), textWidth + S(60), S(18)));

            g.DrawString(
                entry.Score.ToString(),
                listScoreFont,
                scoreBrush,
                new RectangleF(row.Right - S(172), row.Y + S(10), S(148), S(24)),
                rightFormat);
        };

        dialog.Controls.Add(list);

        var closeButton = new Button
        {
            Text = "关闭",
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = theme.ButtonBackground,
            ForeColor = theme.ButtonText,
            Font = buttonFont,
            Size = new Size(S(96), S(32)),
            Location = new Point(
                dialog.ClientSize.Width - S(120),
                dialog.ClientSize.Height - S(56)),
            Cursor = Cursors.Hand
        };
        closeButton.FlatAppearance.BorderSize = 1;
        closeButton.FlatAppearance.BorderColor = theme.ButtonBorder;
        closeButton.FlatAppearance.MouseOverBackColor = theme.ButtonHover;
        closeButton.FlatAppearance.MouseDownBackColor = theme.ButtonPressed;
        dialog.Controls.Add(closeButton);
        dialog.AcceptButton = closeButton;
        dialog.CancelButton = closeButton;

        dialog.ShowDialog(this);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int value,
        int valueSize);
}

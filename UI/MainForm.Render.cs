using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Tetris.Game;
using Tetris.Persistence;

namespace Tetris.UI;

public sealed partial class MainForm
{
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        UpdateLayout();
        UiTheme theme = CurrentTheme;

        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        float scale = DeviceDpi / 96f;
        g.ScaleTransform(scale, scale);

        DrawBackground(g, theme);
        DrawHeader(g, theme);
        DrawBoard(g, theme);
        DrawSidePanel(g, theme);
        DrawFooter(g, theme);
    }

    private void DrawBackground(Graphics g, UiTheme theme)
    {
        float width = _layout.Width;
        float height = _layout.Height;
        var area = new RectangleF(0f, 0f, width, height);

        using var background = new LinearGradientBrush(
            area,
            theme.CanvasTop,
            theme.CanvasBottom,
            90f);
        g.FillRectangle(background, area);

        DrawSoftGlow(
            g,
            new RectangleF(width * 0.56f, -height * 0.28f, width * 0.62f, height * 0.62f),
            theme.GlowPrimary);
        DrawSoftGlow(
            g,
            new RectangleF(-width * 0.26f, height * 0.55f, width * 0.7f, height * 0.7f),
            theme.GlowSecondary);
    }

    private void DrawHeader(Graphics g, UiTheme theme)
    {
        RectangleF header = _layout.Header;
        long now = _clock.ElapsedMilliseconds;
        GraphicsState reveal = BeginReveal(g, header, 0, now);

        float logoSize = _layout.Compact ? 44f : 48f;
        var logo = new RectangleF(header.X, header.Y + (header.Height - logoSize) / 2f, logoSize, logoSize);

        DrawSoftShadow(g, logo, 14f, theme.Shadow);
        DrawLogo(g, theme, logo);

        float titleX = logo.Right + 16f;
        float titleY = header.Y - 2f;

        using (var titleBrush = new LinearGradientBrush(
                   new RectangleF(titleX, titleY, 240f, 42f),
                   theme.Accent,
                   theme.AccentAlt,
                   0f))
        {
            g.DrawString("TETRIS", _displayFont, titleBrush, titleX, titleY);
        }

        using (var subtitleBrush = new SolidBrush(theme.TextSecondary))
        {
            g.DrawString(
                "12 × 16 · 经典俄罗斯方块",
                _subtitleFont,
                subtitleBrush,
                titleX + 2f,
                header.Y + 40f);
        }

        DrawStatusChip(g, theme, header, now);
        g.Restore(reveal);
    }

    private void DrawLogo(Graphics g, UiTheme theme, RectangleF rect)
    {
        using var path = RoundedRectangle(rect, 15f);
        using var fill = new LinearGradientBrush(rect, theme.Accent, theme.AccentAlt, 45f);
        using var border = new Pen(UiTheme.WithAlpha(Color.White, theme.IsDark ? 55 : 110), 1f);

        g.FillPath(fill, path);
        g.DrawPath(border, path);

        float block = rect.Width * 0.15f;
        float gap = block * 0.24f;
        float total = block * 3f + gap * 2f;
        float startX = rect.X + (rect.Width - total) / 2f;
        float startY = rect.Y + (rect.Height - (block * 2f + gap)) / 2f;

        using var blockBrush = new SolidBrush(UiTheme.WithAlpha(Color.White, 232));

        DrawLogoBlock(g, blockBrush, startX + block + gap, startY, block);
        DrawLogoBlock(g, blockBrush, startX, startY + block + gap, block);
        DrawLogoBlock(g, blockBrush, startX + block + gap, startY + block + gap, block);
        DrawLogoBlock(g, blockBrush, startX + (block + gap) * 2f, startY + block + gap, block);
    }

    private static void DrawLogoBlock(Graphics g, Brush brush, float x, float y, float size)
    {
        using var path = RoundedRectangle(new RectangleF(x, y, size, size), size * 0.25f);
        g.FillPath(brush, path);
    }

    private void DrawStatusChip(Graphics g, UiTheme theme, RectangleF header, long now)
    {
        (string label, Color color) = _game.State switch
        {
            GameState.Ready => ("待开始", theme.AccentAlt),
            GameState.Running => ("进行中", theme.Success),
            GameState.Paused => ("已暂停", theme.Warning),
            GameState.GameOver => ("已结束", theme.Danger),
            _ => ("进行中", theme.Success)
        };

        float chipWidth = _layout.Compact ? 84f : 100f;
        float chipHeight = 28f;
        var chip = new RectangleF(
            header.Right - chipWidth,
            header.Y + (header.Height - chipHeight) / 2f,
            chipWidth,
            chipHeight);

        using var chipPath = RoundedRectangle(chip, chipHeight / 2f);
        using var chipBrush = new SolidBrush(UiTheme.WithAlpha(color, theme.IsDark ? 28 : 18));
        using var chipPen = new Pen(UiTheme.WithAlpha(color, theme.IsDark ? 82 : 55), 1f);
        g.FillPath(chipBrush, chipPath);
        g.DrawPath(chipPen, chipPath);

        float pulse = 0.55f + 0.45f * MathF.Sin(now / 480f);
        var dotGlow = new RectangleF(chip.X + 11f, chip.Y + chip.Height / 2f - 4f, 8f, 8f);
        using (var glowBrush = new SolidBrush(UiTheme.WithAlpha(color, ScaleAlpha(90, pulse))))
        {
            g.FillEllipse(glowBrush, dotGlow);
        }

        using (var dotBrush = new SolidBrush(color))
        {
            g.FillEllipse(dotBrush, chip.X + 13f, chip.Y + chip.Height / 2f - 2f, 4f, 4f);
        }

        using var textBrush = new SolidBrush(UiTheme.WithAlpha(color, theme.IsDark ? 235 : 220));
        using var format = new StringFormat { LineAlignment = StringAlignment.Center };
        g.DrawString(
            label,
            _captionBoldFont,
            textBrush,
            new RectangleF(chip.X + 23f, chip.Y, chip.Width - 29f, chip.Height),
            format);
    }

    private void DrawBoard(Graphics g, UiTheme theme)
    {
        RectangleF board = _layout.Board;
        float radius = _layout.Compact ? 16f : 22f;
        long now = _clock.ElapsedMilliseconds;

        DrawSoftShadow(g, board, radius, theme.Shadow);
        using var boardPath = RoundedRectangle(board, radius);
        using var boardFill = new LinearGradientBrush(board, theme.BoardTop, theme.BoardBottom, 90f);
        g.FillPath(boardFill, boardPath);

        GraphicsState clip = g.Save();
        g.SetClip(boardPath, CombineMode.Intersect);

        DrawGrid(g, theme);
        DrawLockedBlocks(g, theme);
        DrawLockFlashes(g, theme);
        DrawClearFlashes(g, theme);
        DrawGhostPiece(g, theme);
        DrawCurrentPiece(g, theme);

        DrawBoardOverlays(g, theme, now);
        g.Restore(clip);

        using var boardPen = new Pen(theme.BoardBorder, 1.4f);
        g.DrawPath(boardPen, boardPath);
    }

    private void DrawGrid(Graphics g, UiTheme theme)
    {
        RectangleF board = _layout.Board;
        float cell = _layout.CellSize;
        using var gridPen = new Pen(theme.GridLine, 1f);

        for (int column = 1; column < TetrisGame.Columns; column++)
        {
            float x = board.X + column * cell;
            g.DrawLine(gridPen, x, board.Y, x, board.Bottom);
        }

        for (int row = 1; row < TetrisGame.Rows; row++)
        {
            float y = board.Y + row * cell;
            g.DrawLine(gridPen, board.X, y, board.Right, y);
        }
    }

    private void DrawLockedBlocks(Graphics g, UiTheme theme)
    {
        for (int row = 0; row < TetrisGame.Rows; row++)
        {
            for (int column = 0; column < TetrisGame.Columns; column++)
            {
                TetrominoType? type = _game.GetCell(column, row);

                if (type is null)
                {
                    continue;
                }

                DrawBlock(g, CellRectangle(column, row), theme.BlockColor(type.Value), theme);
            }
        }
    }

    private void DrawLockFlashes(Graphics g, UiTheme theme)
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
            Color color = theme.BlockColor(flash.Type);
            RectangleF rect = CellRectangle(flash.X, flash.Y);
            rect.Inflate(2f + 3f * fade, 2f + 3f * fade);

            using var path = RoundedRectangle(rect, rect.Height * 0.25f);
            using var glow = new SolidBrush(UiTheme.WithAlpha(color, ScaleAlpha(70, fade)));
            using var pen = new Pen(UiTheme.WithAlpha(UiTheme.Blend(color, Color.White, 0.45f), ScaleAlpha(150, fade)), 1.4f);
            g.FillPath(glow, path);
            g.DrawPath(pen, path);
        }
    }

    private void DrawClearFlashes(Graphics g, UiTheme theme)
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

            for (int column = 0; column < TetrisGame.Columns; column++)
            {
                Color color = UiTheme.Blend(
                    theme.BlockColor(flash.Cells[column]),
                    Color.White,
                    whiten * 0.88f);
                using var path = RoundedRectangle(CellRectangle(column, flash.Row), _layout.CellSize * 0.18f);
                using var brush = new SolidBrush(UiTheme.WithAlpha(color, ScaleAlpha(225, fade)));
                g.FillPath(brush, path);
            }
        }
    }

    private void DrawGhostPiece(Graphics g, UiTheme theme)
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
        Color color = theme.BlockColor(piece.Type);
        float pulse = 0.55f + 0.45f * MathF.Sin(_clock.ElapsedMilliseconds / 520f);

        for (int row = 0; row < piece.Size; row++)
        {
            for (int column = 0; column < piece.Size; column++)
            {
                if (piece.Cells[row, column] == 0)
                {
                    continue;
                }

                int boardRow = ghostY + row;

                if (boardRow < 0 || boardRow >= TetrisGame.Rows)
                {
                    continue;
                }

                DrawGhostBlock(g, CellRectangle(_game.CurrentX + column, boardRow), color, pulse);
            }
        }
    }

    private void DrawCurrentPiece(Graphics g, UiTheme theme)
    {
        if (_game.State is GameState.GameOver or GameState.Ready)
        {
            return;
        }

        Tetromino piece = _game.Current;
        Color color = theme.BlockColor(piece.Type);

        for (int row = 0; row < piece.Size; row++)
        {
            for (int column = 0; column < piece.Size; column++)
            {
                if (piece.Cells[row, column] == 0)
                {
                    continue;
                }

                float boardRow = _pieceRenderY + row;

                if (boardRow < 0f || boardRow >= TetrisGame.Rows)
                {
                    continue;
                }

                DrawBlock(g, CellRectangle(_game.CurrentX + column, boardRow), color, theme);
            }
        }
    }

    private void DrawBoardOverlays(Graphics g, UiTheme theme, long now)
    {
        if (_game.State == GameState.Ready)
        {
            DrawStartOverlay(g, theme, 1f);
        }
        else if (_game.State == GameState.Paused)
        {
            DrawPauseOverlay(g, theme, now);
        }
        else if (_game.State == GameState.GameOver)
        {
            DrawGameOverOverlay(g, theme, now);
        }
        else if (_startedAt >= 0)
        {
            float t = (now - _startedAt) / 260f;

            if (t < 1f)
            {
                DrawStartOverlay(g, theme, 1f - EaseInOut(t));
            }
            else
            {
                _startedAt = -1;
            }
        }
    }

    private void DrawStartOverlay(Graphics g, UiTheme theme, float alpha)
    {
        RectangleF board = _layout.Board;
        float centerY = board.Y + board.Height / 2f;
        float contentTop = centerY - 104f;
        float fade = Math.Clamp(alpha, 0f, 1f);

        DrawOverlayBackdrop(g, theme, fade);

        var accentRect = new RectangleF(board.X + board.Width / 2f - 24f, contentTop - 18f, 48f, 4f);
        using (var accentPath = RoundedRectangle(accentRect, 2f))
        using (var accentBrush = new LinearGradientBrush(accentRect, theme.Accent, theme.AccentAlt, 0f))
        {
            g.FillPath(accentBrush, accentPath);
        }

        DrawCenteredText(
            g,
            "准备开始",
            _overlayTitleFont,
            UiTheme.WithAlpha(theme.TextPrimary, ScaleAlpha(255, fade)),
            new RectangleF(board.X, contentTop, board.Width, 38f));

        DrawCenteredText(
            g,
            "12 × 16 · 经典俄罗斯方块",
            _overlayBodyFont,
            UiTheme.WithAlpha(theme.TextSecondary, ScaleAlpha(255, fade)),
            new RectangleF(board.X, contentTop + 40f, board.Width, 24f));

        DrawCenteredText(
            g,
            $"最高纪录  {_highScoreStore.HighScore}",
            _captionBoldFont,
            UiTheme.WithAlpha(theme.Warning, ScaleAlpha(255, fade)),
            new RectangleF(board.X, contentTop + 66f, board.Width, 20f));

        (string Key, string Action)[] hints =
        {
            ("← →", "移动"),
            ("↑ / Z", "旋转"),
            ("↓", "软降"),
            ("SPACE", "瞬降")
        };

        float gridTop = contentTop + 98f;
        float columnWidth = (board.Width - 32f) / 2f;
        var keyBrush = new SolidBrush(UiTheme.WithAlpha(theme.Accent, ScaleAlpha(235, fade)));
        var descBrush = new SolidBrush(UiTheme.WithAlpha(theme.TextSecondary, ScaleAlpha(220, fade)));

        try
        {
            for (int i = 0; i < hints.Length; i++)
            {
                int column = i % 2;
                int row = i / 2;
                float x = board.X + 16f + column * columnWidth;
                float y = gridTop + row * 24f;

                g.DrawString(hints[i].Key, _keyFont, keyBrush, x, y);
                g.DrawString(hints[i].Action, _captionFont, descBrush, x + 48f, y + 1f);
            }
        }
        finally
        {
            keyBrush.Dispose();
            descBrush.Dispose();
        }

        float pulse = 0.75f + 0.25f * MathF.Sin(_clock.ElapsedMilliseconds / 620f);
        DrawCenteredText(
            g,
            "按任意键开始",
            _overlayBodyFont,
            UiTheme.WithAlpha(theme.Accent, ScaleAlpha(ScaleAlpha(255, fade), pulse)),
            new RectangleF(board.X, gridTop + 58f, board.Width, 24f));
    }

    private void DrawPauseOverlay(Graphics g, UiTheme theme, long now)
    {
        RectangleF board = _layout.Board;
        float t = _pauseSince < 0
            ? 1f
            : Math.Clamp((now - _pauseSince) / 200f, 0f, 1f);
        float fade = EaseOutCubic(t);
        float centerY = board.Y + board.Height / 2f;

        DrawOverlayBackdrop(g, theme, fade);

        float iconY = centerY - 88f;
        using (var iconBrush = new SolidBrush(UiTheme.WithAlpha(theme.Accent, ScaleAlpha(255, fade))))
        {
            using var left = RoundedRectangle(new RectangleF(board.X + board.Width / 2f - 13f, iconY, 8f, 24f), 4f);
            using var right = RoundedRectangle(new RectangleF(board.X + board.Width / 2f + 5f, iconY, 8f, 24f), 4f);
            g.FillPath(iconBrush, left);
            g.FillPath(iconBrush, right);
        }

        DrawCenteredText(
            g,
            "已暂停",
            _overlayTitleFont,
            UiTheme.WithAlpha(theme.TextPrimary, ScaleAlpha(255, fade)),
            new RectangleF(board.X, centerY - 44f, board.Width, 38f));

        DrawCenteredText(
            g,
            "按 P 继续游戏",
            _overlayBodyFont,
            UiTheme.WithAlpha(theme.TextSecondary, ScaleAlpha(255, fade)),
            new RectangleF(board.X, centerY + 8f, board.Width, 24f));
    }

    private void DrawGameOverOverlay(Graphics g, UiTheme theme, long now)
    {
        RectangleF board = _layout.Board;
        float t = _gameOverSince < 0
            ? 1f
            : Math.Clamp((now - _gameOverSince) / 480f, 0f, 1f);
        float fade = EaseOutCubic(t);
        float slide = (1f - fade) * -12f;
        float centerY = board.Y + board.Height / 2f;

        DrawOverlayBackdrop(g, theme, fade);

        using (var accentPen = new Pen(UiTheme.WithAlpha(theme.Danger, ScaleAlpha(180, fade)), 2f))
        {
            float accentY = centerY - 108f + slide;
            g.DrawLine(
                accentPen,
                board.X + board.Width / 2f - 24f,
                accentY,
                board.X + board.Width / 2f + 24f,
                accentY);
        }

        DrawCenteredText(
            g,
            "游戏结束",
            _overlayTitleFont,
            UiTheme.WithAlpha(theme.Danger, ScaleAlpha(255, fade)),
            new RectangleF(board.X, centerY - 92f + slide, board.Width, 38f));

        DrawCenteredText(
            g,
            "本局得分",
            _captionFont,
            UiTheme.WithAlpha(theme.TextMuted, ScaleAlpha(255, fade)),
            new RectangleF(board.X, centerY - 44f + slide, board.Width, 18f));

        DrawCenteredText(
            g,
            _game.Score.ToString(),
            _scoreFont,
            UiTheme.WithAlpha(theme.TextPrimary, ScaleAlpha(255, fade)),
            new RectangleF(board.X, centerY - 27f + slide, board.Width, 38f));

        DrawCenteredText(
            g,
            $"消行 {_game.Lines}   ·   等级 {_game.Level}",
            _captionFont,
            UiTheme.WithAlpha(theme.TextSecondary, ScaleAlpha(235, fade)),
            new RectangleF(board.X, centerY + 14f + slide, board.Width, 18f));

        DrawCenteredText(
            g,
            $"历史最高  {_highScoreStore.HighScore}",
            _captionBoldFont,
            UiTheme.WithAlpha(theme.Warning, ScaleAlpha(235, fade)),
            new RectangleF(board.X, centerY + 36f + slide, board.Width, 18f));

        DrawCenteredText(
            g,
            "按 R 重新开始",
            _overlayBodyFont,
            UiTheme.WithAlpha(theme.Accent, ScaleAlpha(255, fade)),
            new RectangleF(board.X, centerY + 68f + slide, board.Width, 24f));
    }

    private void DrawOverlayBackdrop(Graphics g, UiTheme theme, float opacity)
    {
        RectangleF board = _layout.Board;
        float radius = _layout.Compact ? 16f : 22f;

        using var overlayPath = RoundedRectangle(board, radius);
        using var overlayBrush = new SolidBrush(
            UiTheme.WithAlpha(theme.OverlayBackdrop, ScaleAlpha(theme.IsDark ? 224 : 235, opacity)));
        g.FillPath(overlayBrush, overlayPath);
    }

    private void DrawSidePanel(Graphics g, UiTheme theme)
    {
        long now = _clock.ElapsedMilliseconds;

        DrawNextCard(g, theme, now);
        DrawScoreCard(g, theme, now);
        DrawLevelAndLinesCards(g, theme, now);

        if (_layout.ShowControls)
        {
            DrawControlsCard(g, theme, now);
        }

        DrawLeaderboardCard(g, theme, now);
    }

    private void DrawNextCard(Graphics g, UiTheme theme, long now)
    {
        RectangleF card = _layout.NextCard;
        GraphicsState reveal = BeginReveal(g, card, 1, now);

        DrawCardSurface(g, theme, card, theme.Accent);
        DrawCardTitle(g, theme, "下一个", card, theme.Accent);

        var preview = new RectangleF(
            card.X + 16f,
            card.Y + 40f,
            card.Width - 32f,
            Math.Max(16f, card.Height - 52f));

        using (var previewPath = RoundedRectangle(preview, 12f))
        using (var previewBrush = new SolidBrush(theme.SurfaceMuted))
        using (var previewPen = new Pen(theme.Border, 1f))
        {
            g.FillPath(previewBrush, previewPath);
            g.DrawPath(previewPen, previewPath);
        }

        Tetromino piece = Tetromino.Create(_game.NextType);
        GetBlockBounds(piece, out int minRow, out int maxRow, out int minCol, out int maxCol);

        float cell = MathF.Min(
            22f,
            MathF.Min((preview.Width - 8f) / 4f, (preview.Height - 8f) / 2f));
        cell = MathF.Max(10f, cell);

        int width = maxCol - minCol + 1;
        int height = maxRow - minRow + 1;
        float startX = preview.X + (preview.Width - width * cell) / 2f;
        float startY = preview.Y + (preview.Height - height * cell) / 2f;
        Color color = theme.BlockColor(piece.Type);

        for (int row = minRow; row <= maxRow; row++)
        {
            for (int column = minCol; column <= maxCol; column++)
            {
                if (piece.Cells[row, column] == 0)
                {
                    continue;
                }

                var rect = new RectangleF(
                    startX + (column - minCol) * cell + 2f,
                    startY + (row - minRow) * cell + 2f,
                    cell - 4f,
                    cell - 4f);
                DrawBlock(g, rect, color, theme);
            }
        }

        g.Restore(reveal);
    }

    private void DrawScoreCard(Graphics g, UiTheme theme, long now)
    {
        RectangleF card = _layout.ScoreCard;
        GraphicsState reveal = BeginReveal(g, card, 2, now);

        DrawCardSurface(g, theme, card, theme.Accent);
        DrawCardTitle(g, theme, "分数", card, theme.Accent);

        using (var recordBrush = new SolidBrush(theme.Warning))
        using (var recordFormat = new StringFormat { Alignment = StringAlignment.Far })
        {
            g.DrawString(
                $"最高 {_highScoreStore.HighScore}",
                _captionBoldFont,
                recordBrush,
                new RectangleF(card.X + 16f, card.Y + 12f, card.Width - 32f, 18f),
                recordFormat);
        }

        float popScale = 1f;

        if (_scorePopAt >= 0)
        {
            float pop = (now - _scorePopAt) / 220f;

            if (pop >= 1f)
            {
                _scorePopAt = -1;
            }
            else
            {
                popScale = 1f + 0.18f * (1f - pop) * (1f - pop);
            }
        }

        string scoreText = _game.Score.ToString();
        using var scoreBrush = new SolidBrush(theme.TextPrimary);
        var scoreOrigin = new PointF(card.X + 16f, card.Y + 32f);
        float scoreWidth = g.MeasureString(scoreText, _scoreFont).Width;
        float fitScale = MathF.Min(1f, (card.Width - 32f) / MathF.Max(1f, scoreWidth));
        float totalScale = popScale * fitScale;

        if (totalScale > 1.001f)
        {
            GraphicsState state = g.Save();
            g.TranslateTransform(scoreOrigin.X, scoreOrigin.Y);
            g.ScaleTransform(totalScale, totalScale);
            g.DrawString(scoreText, _scoreFont, scoreBrush, 0f, 0f);
            g.Restore(state);
        }
        else
        {
            g.DrawString(scoreText, _scoreFont, scoreBrush, scoreOrigin);
        }

        if (_scorePopAt >= 0 && _scoreDelta > 0)
        {
            float rise = (now - _scorePopAt) / 620f;

            if (rise < 1f)
            {
                using var deltaBrush = new SolidBrush(
                    UiTheme.WithAlpha(theme.Warning, ScaleAlpha(235, 1f - rise)));
                using var rightFormat = new StringFormat { Alignment = StringAlignment.Far };
                g.DrawString(
                    $"+{_scoreDelta}",
                    _captionBoldFont,
                    deltaBrush,
                    new RectangleF(card.X + 16f, card.Y + 34f - 14f * rise, card.Width - 32f, 18f),
                    rightFormat);
            }
        }

        g.Restore(reveal);
    }

    private void DrawLevelAndLinesCards(Graphics g, UiTheme theme, long now)
    {
        RectangleF levelCard = _layout.LevelCard;
        RectangleF linesCard = _layout.LinesCard;
        GraphicsState levelReveal = BeginReveal(g, levelCard, 3, now);

        DrawCardSurface(g, theme, levelCard, theme.AccentAlt);
        DrawCardTitle(g, theme, "等级", levelCard, theme.AccentAlt);

        if (_levelUpAt >= 0)
        {
            float glow = (now - _levelUpAt) / 650f;

            if (glow >= 1f)
            {
                _levelUpAt = -1;
            }
            else
            {
                using var glowPath = RoundedRectangle(levelCard, 16f);
                using var glowPen = new Pen(UiTheme.WithAlpha(theme.Accent, ScaleAlpha(190, 1f - glow)), 2f);
                g.DrawPath(glowPen, glowPath);
            }
        }

        using (var valueBrush = new SolidBrush(theme.TextPrimary))
        {
            g.DrawString(
                _game.Level.ToString("D2"),
                _statFont,
                valueBrush,
                levelCard.X + 14f,
                levelCard.Y + 28f);
        }

        g.Restore(levelReveal);

        GraphicsState linesReveal = BeginReveal(g, linesCard, 4, now);
        DrawCardSurface(g, theme, linesCard, theme.Success);
        DrawCardTitle(g, theme, "消行", linesCard, theme.Success);

        using (var valueBrush = new SolidBrush(theme.TextPrimary))
        {
            g.DrawString(
                _game.Lines.ToString("D2"),
                _statFont,
                valueBrush,
                linesCard.X + 14f,
                linesCard.Y + 28f);
        }

        g.Restore(linesReveal);
    }

    private void DrawControlsCard(Graphics g, UiTheme theme, long now)
    {
        RectangleF card = _layout.ControlsCard;
        GraphicsState reveal = BeginReveal(g, card, 5, now);

        DrawCardSurface(g, theme, card, theme.Accent);
        DrawCardTitle(g, theme, "操作", card, theme.Accent);

        (string Key, string Action)[] rows =
        {
            ("← →", "移动"),
            ("↑ / Z", "旋转"),
            ("↓ / Space", "软降 · 瞬降"),
            ("P / R", "暂停 · 重开")
        };

        float rowY = card.Y + 32f;

        for (int i = 0; i < rows.Length; i++)
        {
            bool active = i switch
            {
                0 => _pressed.Contains(Keys.Left) || _pressed.Contains(Keys.Right),
                1 => _pressed.Contains(Keys.Up) || _pressed.Contains(Keys.Z),
                2 => _pressed.Contains(Keys.Down) || _pressed.Contains(Keys.Space),
                3 => _pressed.Contains(Keys.P) || _pressed.Contains(Keys.R),
                _ => false
            };

            float keyWidth = MathF.Max(
                44f,
                g.MeasureString(rows[i].Key, _keyFont).Width + 14f);
            var keyRect = new RectangleF(card.X + 16f, rowY + 2f, keyWidth, 20f);

            using (var keyPath = RoundedRectangle(keyRect, 6f))
            using (var keyFill = new SolidBrush(active
                       ? UiTheme.WithAlpha(theme.Accent, theme.IsDark ? 42 : 28)
                       : theme.SurfaceMuted))
            using (var keyPen = new Pen(active ? UiTheme.WithAlpha(theme.Accent, 120) : theme.Border, 1f))
            {
                g.FillPath(keyFill, keyPath);
                g.DrawPath(keyPen, keyPath);
            }

            using (var keyTextBrush = new SolidBrush(active ? theme.Accent : theme.TextSecondary))
            using (var keyFormat = new StringFormat
                   {
                       Alignment = StringAlignment.Center,
                       LineAlignment = StringAlignment.Center
                   })
            {
                g.DrawString(rows[i].Key, _keyFont, keyTextBrush, keyRect, keyFormat);
            }

            using var actionBrush = new SolidBrush(theme.TextSecondary);
            using var actionFormat = new StringFormat { LineAlignment = StringAlignment.Center };
            g.DrawString(
                rows[i].Action,
                _captionFont,
                actionBrush,
                new RectangleF(keyRect.Right + 8f, rowY + 2f, card.Right - keyRect.Right - 20f, 20f),
                actionFormat);

            rowY += 24f;
        }

        g.Restore(reveal);
    }

    private void DrawLeaderboardCard(Graphics g, UiTheme theme, long now)
    {
        RectangleF card = _layout.LeaderboardCard;
        GraphicsState reveal = BeginReveal(g, card, 6, now);

        DrawCardSurface(g, theme, card, theme.Warning);
        DrawCardTitle(g, theme, "排行榜", card, theme.Warning);

        DrawHistoryButton(g, theme);

        float rowTop = card.Y + 40f;
        float rowHeight = MathF.Min(22f, MathF.Max(14f, (card.Bottom - rowTop - 12f) / 3f));

        if (_topScores is null || _topScores.Status == ScoreboardStatus.IoError)
        {
            DrawLeaderboardEmptyState(g, theme, "排行榜不可用", "请稍后重试");
        }
        else if (_topScores.Status == ScoreboardStatus.Empty)
        {
            DrawLeaderboardEmptyState(g, theme, "还没有成绩", "完成一局后自动记录");
        }
        else if (_topScores.Status == ScoreboardStatus.Corrupted)
        {
            DrawLeaderboardEmptyState(g, theme, "数据需要修复", "历史记录暂不可读");
        }
        else
        {
            int count = Math.Min(3, _topScores.Entries.Count);
            using var rankFormat = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            using var scoreFormat = new StringFormat { Alignment = StringAlignment.Far };
            using var rowFormat = new StringFormat { LineAlignment = StringAlignment.Center };

            for (int i = 0; i < count; i++)
            {
                ScoreEntry entry = _topScores.Entries[i];
                float y = rowTop + i * rowHeight;
                Color rankColor = i switch
                {
                    0 => theme.Warning,
                    1 => theme.TextSecondary,
                    2 => theme.Accent,
                    _ => theme.TextMuted
                };

                var badge = new RectangleF(card.X + 16f, y, 16f, rowHeight - 2f);

                using (var badgePath = RoundedRectangle(badge, 5f))
                using (var badgeFill = new SolidBrush(UiTheme.WithAlpha(rankColor, theme.IsDark ? 40 : 24)))
                {
                    g.FillPath(badgeFill, badgePath);
                }

                using (var rankBrush = new SolidBrush(rankColor))
                {
                    g.DrawString((i + 1).ToString(), _captionBoldFont, rankBrush, badge, rankFormat);
                }

                float nameX = card.X + 40f;
                float scoreWidth = 56f;
                float nameWidth = MathF.Max(24f, card.Width - (nameX - card.X) - scoreWidth - 16f);
                string name = Ellipsize(g, entry.PlayerName, _captionFont, nameWidth);

                using (var nameBrush = new SolidBrush(theme.TextSecondary))
                {
                    g.DrawString(
                        name,
                        _captionFont,
                        nameBrush,
                        new RectangleF(nameX, y, nameWidth, rowHeight),
                        rowFormat);
                }

                using (var scoreBrush = new SolidBrush(theme.TextSecondary))
                {
                    g.DrawString(
                        entry.Score.ToString(),
                        _captionBoldFont,
                        scoreBrush,
                        new RectangleF(card.Right - 16f - scoreWidth, y, scoreWidth, rowHeight),
                        scoreFormat);
                }
            }
        }

        g.Restore(reveal);
    }

    private void DrawLeaderboardEmptyState(
        Graphics g,
        UiTheme theme,
        string title,
        string subtitle)
    {
        RectangleF card = _layout.LeaderboardCard;
        float top = card.Y + 48f;
        float height = MathF.Max(32f, card.Height - 60f);

        DrawCenteredText(
            g,
            title,
            _captionBoldFont,
            theme.TextSecondary,
            new RectangleF(card.X + 16f, top, card.Width - 32f, 20f));

        DrawCenteredText(
            g,
            subtitle,
            _captionFont,
            theme.TextMuted,
            new RectangleF(card.X + 16f, top + 20f, card.Width - 32f, height - 20f));
    }

    private void DrawHistoryButton(Graphics g, UiTheme theme)
    {
        RectangleF rect = _layout.HistoryButton;
        bool enabled = HistoryButtonEnabled;
        float hover = enabled ? _historyHoverProgress : 0f;

        if (_historyPressed && enabled)
        {
            rect.Inflate(-1f, -1f);
        }

        if (hover > 0.02f)
        {
            DrawSoftShadow(g, rect, 12f, UiTheme.WithAlpha(theme.Shadow, (int)(theme.Shadow.A * hover)));
        }

        Color fill = !enabled
            ? theme.SurfaceMuted
            : _historyPressed
                ? theme.ButtonPressed
                : UiTheme.Blend(theme.ButtonBackground, theme.ButtonHover, hover);

        using var path = RoundedRectangle(rect, 10f);
        using var fillBrush = new SolidBrush(fill);
        using var borderPen = new Pen(
            enabled
                ? UiTheme.WithAlpha(theme.ButtonBorder, theme.IsDark ? 150 : 110)
                : theme.Border,
            1f);
        g.FillPath(fillBrush, path);
        g.DrawPath(borderPen, path);

        using var textBrush = new SolidBrush(enabled ? theme.ButtonText : theme.TextMuted);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString(
            _layout.Compact ? "全部" : "查看全部",
            _buttonFont,
            textBrush,
            rect,
            format);
    }

    private void DrawFooter(Graphics g, UiTheme theme)
    {
        RectangleF footer = _layout.Footer;
        long now = _clock.ElapsedMilliseconds;

        using var leftFormat = new StringFormat
        {
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        using var rightFormat = new StringFormat
        {
            Alignment = StringAlignment.Far,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap
        };

        if (_notice is not null && now < _noticeUntil)
        {
            using var noticeBrush = new SolidBrush(theme.Warning);
            g.DrawString(
                _notice,
                _captionFont,
                noticeBrush,
                new RectangleF(footer.X, footer.Y, footer.Width, footer.Height),
                leftFormat);
            return;
        }

        using var mutedBrush = new SolidBrush(theme.TextMuted);
        g.DrawString(
            "得分 = 100 × 消行数",
            _captionFont,
            mutedBrush,
            new RectangleF(footer.X, footer.Y, footer.Width * 0.52f, footer.Height),
            leftFormat);

        g.DrawString(
            "P 暂停  ·  R 重开",
            _captionFont,
            mutedBrush,
            new RectangleF(footer.X + footer.Width * 0.48f, footer.Y, footer.Width * 0.52f, footer.Height),
            rightFormat);
    }

    private void DrawCardSurface(Graphics g, UiTheme theme, RectangleF rect, Color accent)
    {
        DrawSoftShadow(g, rect, 18f, theme.Shadow);

        using var path = RoundedRectangle(rect, 16f);
        using var fill = new LinearGradientBrush(rect, theme.SurfaceTop, theme.SurfaceBottom, 90f);
        using var border = new Pen(
            UiTheme.WithAlpha(accent, theme.IsDark ? 58 : 44),
            1f);

        g.FillPath(fill, path);
        g.DrawPath(border, path);

        using var highlight = new Pen(UiTheme.WithAlpha(Color.White, theme.IsDark ? 18 : 60), 1f);
        g.DrawLine(
            highlight,
            rect.X + 14f,
            rect.Y + 1.2f,
            rect.Right - 14f,
            rect.Y + 1.2f);
    }

    private void DrawCardTitle(
        Graphics g,
        UiTheme theme,
        string text,
        RectangleF card,
        Color accent)
    {
        var dot = new RectangleF(card.X + 16f, card.Y + 17f, 6f, 6f);

        using (var dotBrush = new SolidBrush(UiTheme.WithAlpha(accent, 220)))
        {
            g.FillEllipse(dotBrush, dot);
        }

        using var textBrush = new SolidBrush(theme.TextSecondary);
        g.DrawString(text, _cardTitleFont, textBrush, card.X + 28f, card.Y + 12f);
    }

    private static void DrawBlock(Graphics g, RectangleF rect, Color color, UiTheme theme)
    {
        if (rect.Width <= 1f || rect.Height <= 1f)
        {
            return;
        }

        float radius = MathF.Max(3f, rect.Height * 0.2f);
        using var path = RoundedRectangle(rect, radius);

        Color top = UiTheme.Blend(color, Color.White, theme.IsDark ? 0.22f : 0.12f);
        Color bottom = UiTheme.Blend(color, Color.Black, theme.IsDark ? 0.10f : 0.05f);

        using (var fill = new LinearGradientBrush(rect, top, bottom, 90f))
        {
            g.FillPath(fill, path);
        }

        using (var border = new Pen(
                   UiTheme.WithAlpha(UiTheme.Blend(color, Color.White, 0.35f), theme.IsDark ? 115 : 85),
                   1f))
        {
            g.DrawPath(border, path);
        }

        var glossRect = new RectangleF(
            rect.X + MathF.Max(2f, rect.Width * 0.08f),
            rect.Y + MathF.Max(2f, rect.Height * 0.08f),
            rect.Width - MathF.Max(4f, rect.Width * 0.16f),
            rect.Height * 0.3f);

        using var glossPath = RoundedRectangle(glossRect, MathF.Min(3f, glossRect.Height / 2f));
        using var glossBrush = new LinearGradientBrush(
            glossRect,
            UiTheme.WithAlpha(Color.White, theme.IsDark ? 52 : 75),
            Color.FromArgb(0, Color.White),
            90f);
        g.FillPath(glossBrush, glossPath);
    }

    private static void DrawGhostBlock(Graphics g, RectangleF rect, Color color, float pulse)
    {
        using var path = RoundedRectangle(rect, MathF.Max(3f, rect.Height * 0.18f));
        using var fill = new SolidBrush(UiTheme.WithAlpha(color, 12));
        g.FillPath(fill, path);

        using var pen = new Pen(UiTheme.WithAlpha(color, ScaleAlpha(72, pulse)), 1.2f)
        {
            DashStyle = DashStyle.Dash
        };
        g.DrawPath(pen, path);
    }

    private GraphicsState BeginReveal(Graphics g, RectangleF rect, int index, long now)
    {
        GraphicsState state = g.Save();
        float t = Math.Clamp((now - _shownAt - index * 45f) / (float)CardRevealMs, 0f, 1f);

        if (t >= 1f || rect.Width <= 0f || rect.Height <= 0f)
        {
            return state;
        }

        float ease = EaseOutCubic(t);
        float scale = 0.975f + 0.025f * ease;
        float offsetY = 14f * (1f - ease);
        float centerX = rect.X + rect.Width / 2f;
        float centerY = rect.Y + rect.Height / 2f;

        g.TranslateTransform(centerX, centerY);
        g.ScaleTransform(scale, scale);
        g.TranslateTransform(-centerX, -centerY + offsetY);
        return state;
    }

    private void DrawCenteredText(
        Graphics g,
        string text,
        Font font,
        Color color,
        RectangleF area)
    {
        using var brush = new SolidBrush(color);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString(text, font, brush, area, format);
    }

    private static void DrawSoftGlow(Graphics g, RectangleF rect, Color color)
    {
        if (rect.Width <= 0f || rect.Height <= 0f)
        {
            return;
        }

        using var path = new GraphicsPath();
        path.AddEllipse(rect);
        using var brush = new PathGradientBrush(path)
        {
            CenterColor = color,
            SurroundColors = new[] { Color.FromArgb(0, color) }
        };
        g.FillPath(brush, path);
    }

    private static void DrawSoftShadow(Graphics g, RectangleF rect, float radius, Color shadow)
    {
        if (rect.Width <= 0f || rect.Height <= 0f || shadow.A == 0)
        {
            return;
        }

        for (int i = 3; i >= 1; i--)
        {
            RectangleF layer = rect;
            layer.Inflate(i * 2f, i * 2f);
            layer.Offset(0f, 7f);

            using var path = RoundedRectangle(layer, radius + i * 2f);
            using var brush = new SolidBrush(UiTheme.WithAlpha(shadow, Math.Max(1, shadow.A / (i * 2))));
            g.FillPath(brush, path);
        }
    }

    private RectangleF CellRectangle(int column, float row)
    {
        RectangleF board = _layout.Board;
        float cell = _layout.CellSize;
        float inset = MathF.Max(2f, cell * 0.075f);

        return new RectangleF(
            board.X + column * cell + inset,
            board.Y + row * cell + inset,
            cell - inset * 2f,
            cell - inset * 2f);
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
            for (int column = 0; column < piece.Size; column++)
            {
                if (piece.Cells[row, column] == 0)
                {
                    continue;
                }

                minRow = Math.Min(minRow, row);
                maxRow = Math.Max(maxRow, row);
                minCol = Math.Min(minCol, column);
                maxCol = Math.Max(maxCol, column);
            }
        }
    }

    private static string Ellipsize(Graphics g, string text, Font font, float maxWidth)
    {
        if (maxWidth <= 0f || g.MeasureString(text, font).Width <= maxWidth)
        {
            return text;
        }

        for (int length = text.Length - 1; length > 0; length--)
        {
            string candidate = text[..length] + "…";

            if (g.MeasureString(candidate, font).Width <= maxWidth)
            {
                return candidate;
            }
        }

        return "…";
    }

    private static Font PixelFont(string family, float pixelSize, FontStyle style = FontStyle.Regular)
    {
        return new Font(family, pixelSize, style, GraphicsUnit.Pixel);
    }

    private static int ScaleAlpha(int alpha, float scale)
    {
        return (int)Math.Clamp(alpha * scale, 0f, 255f);
    }

    private static float EaseOutCubic(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInOut(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static GraphicsPath RoundedRectangle(RectangleF rect, float radius)
    {
        radius = MathF.Min(radius, MathF.Min(rect.Width, rect.Height) / 2f);
        radius = MathF.Max(0.1f, radius);
        float diameter = radius * 2f;
        var path = new GraphicsPath();

        path.AddArc(rect.X, rect.Y, diameter, diameter, 180f, 90f);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270f, 90f);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();

        return path;
    }

    private sealed class LockFlash
    {
        public required int X { get; init; }
        public required int Y { get; init; }
        public required TetrominoType Type { get; init; }
        public required long Start { get; init; }
    }

    private sealed class ClearFlash
    {
        public required int Row { get; init; }
        public required TetrominoType[] Cells { get; init; }
        public required long Start { get; init; }
    }
}

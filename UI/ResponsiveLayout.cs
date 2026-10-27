namespace Tetris.UI;

/// <summary>
/// 响应式布局：以 8px 为基准网格，根据窗口宽高切换宽屏/紧凑布局。
/// </summary>
internal sealed class ResponsiveLayout
{
    public required RectangleF Header { get; init; }
    public required RectangleF Board { get; init; }
    public required RectangleF Footer { get; init; }
    public required RectangleF NextCard { get; init; }
    public required RectangleF ScoreCard { get; init; }
    public required RectangleF LevelCard { get; init; }
    public required RectangleF LinesCard { get; init; }
    public required RectangleF ControlsCard { get; init; }
    public required RectangleF LeaderboardCard { get; init; }
    public required RectangleF HistoryButton { get; init; }
    public required float CellSize { get; init; }
    public required float CardGap { get; init; }
    public required bool Compact { get; init; }
    public required bool ShowControls { get; init; }
    public required float Margin { get; init; }
    public required float SideWidth { get; init; }
    public required float Width { get; init; }
    public required float Height { get; init; }

    public static ResponsiveLayout Create(float width, float height)
    {
        width = MathF.Max(width, 400f);
        height = MathF.Max(height, 480f);

        bool compact = width < 640f;
        float margin = compact ? 16f : width >= 900f ? 32f : 24f;
        float headerTop = compact ? 16f : 24f;
        float headerHeight = 64f;
        float boardTop = headerTop + headerHeight + 16f;
        float footerHeight = 24f;
        float bottomMargin = compact ? 16f : 24f;
        float sideWidth = compact ? 160f : width >= 900f ? 224f : 200f;
        float gap = compact ? 16f : width >= 900f ? 24f : 16f;

        float availableWidth = width - margin * 2f - gap - sideWidth;
        float availableHeight = height - boardTop - 16f - footerHeight - bottomMargin;

        float cellSize = MathF.Floor(MathF.Min(availableWidth / 12f, availableHeight / 16f));
        cellSize = Math.Clamp(cellSize, 16f, 40f);

        float boardWidth = cellSize * 12f;
        float boardHeight = cellSize * 16f;
        float groupWidth = boardWidth + gap + sideWidth;
        float groupX = Snap8((width - groupWidth) / 2f);
        groupX = MathF.Max(margin, groupX);

        var board = new RectangleF(groupX, boardTop, boardWidth, boardHeight);
        float sideX = board.Right + gap;
        var header = new RectangleF(groupX, headerTop, groupWidth, headerHeight);
        var footer = new RectangleF(
            board.X,
            board.Bottom + 16f,
            board.Width,
            footerHeight);

        bool showControls = !compact && boardHeight >= 544f;
        int cardCount = showControls ? 5 : 4;
        float cardGap = boardHeight >= 480f ? 16f : 8f;
        float contentHeight = boardHeight - cardGap * (cardCount - 1);

        float nextRatio = showControls ? 0.20f : 0.26f;
        float scoreRatio = showControls ? 0.18f : 0.24f;
        float statsRatio = showControls ? 0.14f : 0.16f;
        float controlsRatio = 0.26f;

        float nextHeight = Snap8(contentHeight * nextRatio);
        float scoreHeight = Snap8(contentHeight * scoreRatio);
        float statsHeight = Snap8(contentHeight * statsRatio);
        float controlsHeight = showControls ? Snap8(contentHeight * controlsRatio) : 0f;
        float leaderHeight = contentHeight - nextHeight - scoreHeight - statsHeight - controlsHeight;

        float y = boardTop;

        var next = new RectangleF(sideX, y, sideWidth, nextHeight);
        y += nextHeight + cardGap;

        var score = new RectangleF(sideX, y, sideWidth, scoreHeight);
        y += scoreHeight + cardGap;

        float statsGap = 8f;
        float statWidth = (sideWidth - statsGap) / 2f;
        var level = new RectangleF(sideX, y, statWidth, statsHeight);
        var lines = new RectangleF(sideX + statWidth + statsGap, y, statWidth, statsHeight);
        y += statsHeight + cardGap;

        RectangleF controls = RectangleF.Empty;

        if (showControls)
        {
            controls = new RectangleF(sideX, y, sideWidth, controlsHeight);
            y += controlsHeight + cardGap;
        }

        var leaderboard = new RectangleF(sideX, y, sideWidth, leaderHeight);
        float buttonWidth = compact ? 44f : 56f;
        float buttonHeight = 24f;
        var historyButton = new RectangleF(
            leaderboard.Right - 16f - buttonWidth,
            leaderboard.Top + 12f,
            buttonWidth,
            buttonHeight);

        return new ResponsiveLayout
        {
            Header = header,
            Board = board,
            Footer = footer,
            NextCard = next,
            ScoreCard = score,
            LevelCard = level,
            LinesCard = lines,
            ControlsCard = controls,
            LeaderboardCard = leaderboard,
            HistoryButton = historyButton,
            CellSize = cellSize,
            CardGap = cardGap,
            Compact = compact,
            ShowControls = showControls,
            Margin = margin,
            SideWidth = sideWidth,
            Width = width,
            Height = height
        };
    }

    private static float Snap8(float value)
    {
        return MathF.Round(value / 8f) * 8f;
    }
}

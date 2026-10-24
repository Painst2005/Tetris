namespace Tetris.Game;

/// <summary>
/// 与界面无关的完整游戏状态快照，可用于独立创建、保存或复制游戏。
/// </summary>
public sealed class GameSnapshot
{
    public GameSnapshot(
        TetrominoType?[,] board,
        Tetromino current,
        int currentX,
        int currentY,
        IEnumerable<TetrominoType> upcoming,
        int score,
        int level,
        int lines,
        GameState state,
        int gravityElapsed = 0)
    {
        ArgumentNullException.ThrowIfNull(board);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(upcoming);

        if (board.GetLength(0) != TetrisGame.Rows || board.GetLength(1) != TetrisGame.Columns)
        {
            throw new ArgumentException(
                $"棋盘尺寸必须为 {TetrisGame.Rows} 行 × {TetrisGame.Columns} 列。",
                nameof(board));
        }

        if (score < 0 || level < 1 || lines < 0 || gravityElapsed < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(score),
                "分数、等级、消行数和重力累计时间必须为非负值。");
        }

        Board = new TetrominoType?[TetrisGame.Rows, TetrisGame.Columns];

        for (int row = 0; row < TetrisGame.Rows; row++)
        {
            for (int column = 0; column < TetrisGame.Columns; column++)
            {
                Board[row, column] = board[row, column];
            }
        }

        Current = current.Clone();
        CurrentX = currentX;
        CurrentY = currentY;
        Upcoming = upcoming.ToArray();
        Score = score;
        Level = level;
        Lines = lines;
        State = state;
        GravityElapsed = gravityElapsed;
    }

    public TetrominoType?[,] Board { get; }

    public Tetromino Current { get; }

    public int CurrentX { get; }

    public int CurrentY { get; }

    public TetrominoType[] Upcoming { get; }

    public int Score { get; }

    public int Level { get; }

    public int Lines { get; }

    public GameState State { get; }

    public int GravityElapsed { get; }

    public GameSnapshot Clone()
    {
        return new GameSnapshot(
            Board,
            Current,
            CurrentX,
            CurrentY,
            Upcoming,
            Score,
            Level,
            Lines,
            State,
            GravityElapsed);
    }
}

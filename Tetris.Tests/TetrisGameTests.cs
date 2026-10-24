using Tetris.Game;

namespace Tetris.Tests;

public sealed class TetrisGameTests
{
    private static readonly TetrominoType[] DefaultUpcoming =
    {
        TetrominoType.O, TetrominoType.S, TetrominoType.T,
        TetrominoType.I, TetrominoType.L, TetrominoType.J, TetrominoType.Z
    };

    [Fact]
    public void MoveLeft_StopsAtLeftWall()
    {
        TetrisGame game = CreateGame(current: TetrominoType.T, currentX: 0, currentY: 5);

        Assert.False(game.MoveLeft());
        Assert.Equal(0, game.CurrentX);
    }

    [Fact]
    public void MoveLeft_IsBlockedByLockedCell()
    {
        TetrominoType?[,] board = EmptyBoard();
        board[6, 2] = TetrominoType.Z;

        TetrisGame game = CreateGame(board, TetrominoType.T, currentX: 3, currentY: 5);

        Assert.False(game.MoveLeft());
        Assert.Equal(3, game.CurrentX);
    }

    [Fact]
    public void SoftDrop_LocksPieceAtFloor()
    {
        TetrisGame game = CreateGame(current: TetrominoType.O, currentX: 4, currentY: 14);

        Assert.False(game.SoftDrop());

        Assert.Equal(TetrominoType.O, game.GetCell(4, 14));
        Assert.Equal(TetrominoType.O, game.GetCell(5, 15));
        Assert.Equal(0, game.Score);
    }

    [Fact]
    public void RotateClockwise_UpdatesPieceShape()
    {
        TetrisGame game = CreateGame(current: TetrominoType.T, currentX: 4, currentY: 5);

        Assert.True(game.RotateClockwise());

        Assert.Equal(4, CountCells(game.Current));
        Assert.Equal(1, game.Current.Cells[0, 1]);
        Assert.Equal(1, game.Current.Cells[1, 1]);
        Assert.Equal(1, game.Current.Cells[2, 1]);
        Assert.Equal(1, game.Current.Cells[1, 2]);
        Assert.Equal(0, game.Current.Cells[1, 0]);
    }

    [Fact]
    public void HardDrop_ClearingOneLine_Scores100()
    {
        TetrominoType?[,] board = EmptyBoard();
        FillRow(board, 15, fromColumn: 4);

        TetrisGame game = CreateGame(board, TetrominoType.I, currentX: 0, currentY: 14);
        game.HardDrop();

        Assert.Equal(100, game.Score);
        Assert.Equal(1, game.Lines);
        Assert.Null(game.GetCell(0, 15));
    }

    [Fact]
    public void HardDrop_ClearingTwoLines_Scores200()
    {
        TetrominoType?[,] board = EmptyBoard();
        FillRow(board, 14);
        FillRow(board, 15);

        TetrisGame game = CreateGame(board, TetrominoType.I, currentX: 4, currentY: 0);
        game.HardDrop();

        Assert.Equal(200, game.Score);
        Assert.Equal(2, game.Lines);
    }

    [Fact]
    public void HardDrop_ClearingFourLines_Scores400()
    {
        TetrominoType?[,] board = EmptyBoard();

        for (int row = 12; row <= 15; row++)
        {
            FillRow(board, row);
        }

        TetrisGame game = CreateGame(board, TetrominoType.I, currentX: 4, currentY: 0);
        game.HardDrop();

        Assert.Equal(400, game.Score);
        Assert.Equal(4, game.Lines);
        Assert.Equal(1, game.Level);
    }

    [Fact]
    public void ClearingTenLines_LevelsUpAndSpeedsUp()
    {
        TetrominoType?[,] board = EmptyBoard();

        for (int row = 6; row <= 15; row++)
        {
            FillRow(board, row);
        }

        TetrisGame game = CreateGame(board, TetrominoType.I, currentX: 4, currentY: 0);
        int initialInterval = game.DropIntervalMs;

        game.HardDrop();

        Assert.Equal(1000, game.Score);
        Assert.Equal(10, game.Lines);
        Assert.Equal(2, game.Level);
        Assert.True(game.DropIntervalMs < initialInterval);
    }

    [Fact]
    public void Clone_CreatesIndependentGameState()
    {
        TetrominoType?[,] board = EmptyBoard();
        board[10, 10] = TetrominoType.Z;

        TetrisGame game = CreateGame(
            board,
            TetrominoType.I,
            currentX: 4,
            currentY: 4,
            score: 300,
            lines: 3);

        TetrisGame clone = game.Clone();

        Assert.NotSame(game.Current, clone.Current);
        Assert.Equal(300, clone.Score);
        Assert.Equal(3, clone.Lines);
        Assert.Equal(4, clone.CurrentY);
        Assert.Equal(TetrominoType.Z, clone.GetCell(10, 10));

        game.HardDrop();

        Assert.Equal(TetrominoType.I, game.GetCell(4, 15));
        Assert.Null(clone.GetCell(4, 15));
        Assert.Equal(4, clone.CurrentY);
        Assert.Equal(300, clone.Score);
    }

    [Fact]
    public void Snapshot_DeepCopiesBoardAndCanCreateGameWithoutUi()
    {
        TetrominoType?[,] board = EmptyBoard();
        board[8, 8] = TetrominoType.T;

        var snapshot = new GameSnapshot(
            board,
            Tetromino.Create(TetrominoType.J),
            currentX: 2,
            currentY: 3,
            upcoming: new[] { TetrominoType.I, TetrominoType.O },
            score: 50,
            level: 1,
            lines: 0,
            state: GameState.Running);

        board[8, 8] = null;

        Assert.Equal(TetrominoType.T, snapshot.Board[8, 8]);

        var game = new TetrisGame(snapshot);

        Assert.Equal(TetrominoType.T, game.GetCell(8, 8));
        Assert.Equal(TetrominoType.J, game.Current.Type);
        Assert.Equal(2, game.CurrentX);
        Assert.Equal(3, game.CurrentY);
        Assert.Equal(50, game.Score);
    }

    [Fact]
    public void SpawnCollision_EndsGame()
    {
        TetrominoType?[,] board = EmptyBoard();

        for (int column = 4; column <= 7; column++)
        {
            board[1, column] = TetrominoType.T;
        }

        TetrisGame game = CreateGame(
            board,
            TetrominoType.I,
            currentX: 4,
            currentY: 5,
            upcoming: new[] { TetrominoType.O, TetrominoType.S, TetrominoType.T });

        game.HardDrop();

        Assert.Equal(GameState.GameOver, game.State);
        Assert.IsType<Tetromino>(game.Current);
    }

    private static TetrisGame CreateGame(
        TetrominoType?[,]? board = null,
        TetrominoType current = TetrominoType.T,
        int currentX = 4,
        int currentY = 0,
        int score = 0,
        int level = 1,
        int lines = 0,
        IEnumerable<TetrominoType>? upcoming = null)
    {
        var snapshot = new GameSnapshot(
            board ?? EmptyBoard(),
            Tetromino.Create(current),
            currentX,
            currentY,
            upcoming ?? DefaultUpcoming,
            score,
            level,
            lines,
            GameState.Running);

        return new TetrisGame(snapshot);
    }

    private static TetrominoType?[,] EmptyBoard()
    {
        return new TetrominoType?[TetrisGame.Rows, TetrisGame.Columns];
    }

    private static void FillRow(
        TetrominoType?[,] board,
        int row,
        int fromColumn = 0,
        TetrominoType type = TetrominoType.T)
    {
        for (int column = fromColumn; column < TetrisGame.Columns; column++)
        {
            board[row, column] = type;
        }
    }

    private static int CountCells(Tetromino piece)
    {
        int count = 0;

        for (int row = 0; row < piece.Size; row++)
        {
            for (int column = 0; column < piece.Size; column++)
            {
                count += piece.Cells[row, column];
            }
        }

        return count;
    }
}

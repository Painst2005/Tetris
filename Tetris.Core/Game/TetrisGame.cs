namespace Tetris.Game;

public enum GameState
{
    Running,
    Paused,
    GameOver
}

/// <summary>
/// 俄罗斯方块核心逻辑：棋盘、方块生成、移动、旋转、消行、计分与等级。
/// </summary>
public sealed class TetrisGame
{
    public const int Columns = 12;
    public const int Rows = 16;

    private static readonly (int X, int Y)[] RotationKicks =
    {
        (0, 0), (-1, 0), (1, 0), (-2, 0), (2, 0),
        (0, -1), (-1, -1), (1, -1)
    };

    private readonly TetrominoType?[,] _board = new TetrominoType?[Rows, Columns];
    private readonly Queue<TetrominoType> _queue = new();
    private readonly Random _random = new();

    private int _gravityElapsed;

    public TetrisGame()
    {
        Reset();
    }

    public TetrisGame(GameSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        RestoreSnapshot(snapshot);
    }

    /// <summary>捕获当前状态，生成的快照与游戏实例互不共享数据。</summary>
    public GameSnapshot CaptureSnapshot()
    {
        return new GameSnapshot(
            _board,
            Current,
            CurrentX,
            CurrentY,
            _queue,
            Score,
            Level,
            Lines,
            State,
            _gravityElapsed);
    }

    /// <summary>创建当前游戏状态的深拷贝，可独立继续运行。</summary>
    public TetrisGame Clone()
    {
        return new TetrisGame(CaptureSnapshot());
    }

    public GameState State { get; private set; }

    public int Score { get; private set; }

    public int Level { get; private set; }

    public int Lines { get; private set; }

    public Tetromino Current { get; private set; } = null!;

    public int CurrentX { get; private set; }

    public int CurrentY { get; private set; }

    public TetrominoType NextType
    {
        get
        {
            EnsureQueue();
            return _queue.Peek();
        }
    }

    public int DropIntervalMs => Math.Max(70, 720 - (Level - 1) * 65);

    /// <summary>幽灵方块（当前方块落点）的纵坐标。</summary>
    public int GhostY
    {
        get
        {
            int y = CurrentY;

            while (CanPlace(Current, CurrentX, y + 1))
            {
                y++;
            }

            return y;
        }
    }

    public TetrominoType? GetCell(int column, int row)
    {
        return _board[row, column];
    }

    public void Reset()
    {
        Array.Clear(_board);
        _queue.Clear();

        Score = 0;
        Lines = 0;
        Level = 1;
        _gravityElapsed = 0;
        State = GameState.Running;

        RefillQueue();
        SpawnPiece();
    }

    public void TogglePause()
    {
        if (State == GameState.Running)
        {
            State = GameState.Paused;
        }
        else if (State == GameState.Paused)
        {
            State = GameState.Running;
            _gravityElapsed = 0;
        }
    }

    public void Update(int elapsedMs)
    {
        if (State != GameState.Running)
        {
            return;
        }

        _gravityElapsed += elapsedMs;
        int interval = DropIntervalMs;

        while (_gravityElapsed >= interval)
        {
            _gravityElapsed -= interval;

            if (!TryMove(0, 1))
            {
                LockPiece();
                break;
            }

            interval = DropIntervalMs;
        }
    }

    public bool MoveLeft()
    {
        return State == GameState.Running && TryMove(-1, 0);
    }

    public bool MoveRight()
    {
        return State == GameState.Running && TryMove(1, 0);
    }

    public bool SoftDrop()
    {
        if (State != GameState.Running)
        {
            return false;
        }

        if (TryMove(0, 1))
        {
            _gravityElapsed = 0;
            return true;
        }

        LockPiece();
        return false;
    }

    public void HardDrop()
    {
        if (State != GameState.Running)
        {
            return;
        }

        while (TryMove(0, 1))
        {
        }

        LockPiece();
    }

    public bool RotateClockwise()
    {
        return TryRotate(clockwise: true);
    }

    public bool RotateCounterClockwise()
    {
        return TryRotate(clockwise: false);
    }

    private bool TryRotate(bool clockwise)
    {
        if (State != GameState.Running)
        {
            return false;
        }

        Tetromino rotated = clockwise
            ? Current.RotateClockwise()
            : Current.RotateCounterClockwise();

        foreach ((int offsetX, int offsetY) in RotationKicks)
        {
            int x = CurrentX + offsetX;
            int y = CurrentY + offsetY;

            if (!CanPlace(rotated, x, y))
            {
                continue;
            }

            Current = rotated;
            CurrentX = x;
            CurrentY = y;
            return true;
        }

        return false;
    }

    private bool TryMove(int deltaX, int deltaY)
    {
        int x = CurrentX + deltaX;
        int y = CurrentY + deltaY;

        if (!CanPlace(Current, x, y))
        {
            return false;
        }

        CurrentX = x;
        CurrentY = y;
        return true;
    }

    private bool CanPlace(Tetromino piece, int originX, int originY)
    {
        int size = piece.Size;

        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                if (piece.Cells[row, col] == 0)
                {
                    continue;
                }

                int x = originX + col;
                int y = originY + row;

                if (x < 0 || x >= Columns || y >= Rows)
                {
                    return false;
                }

                if (y >= 0 && _board[y, x] != null)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void LockPiece()
    {
        int size = Current.Size;

        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                if (Current.Cells[row, col] == 0)
                {
                    continue;
                }

                int x = CurrentX + col;
                int y = CurrentY + row;

                if (y >= 0 && y < Rows && x >= 0 && x < Columns)
                {
                    _board[y, x] = Current.Type;
                }
            }
        }

        int cleared = ClearFullLines();

        if (cleared > 0)
        {
            Score += 100 * cleared;
            Lines += cleared;
            Level = 1 + Lines / 10;
        }

        SpawnPiece();
    }

    private int ClearFullLines()
    {
        int cleared = 0;

        for (int row = Rows - 1; row >= 0; row--)
        {
            if (!IsRowFull(row))
            {
                continue;
            }

            for (int moveRow = row; moveRow > 0; moveRow--)
            {
                for (int col = 0; col < Columns; col++)
                {
                    _board[moveRow, col] = _board[moveRow - 1, col];
                }
            }

            for (int col = 0; col < Columns; col++)
            {
                _board[0, col] = null;
            }

            cleared++;
            row++;
        }

        return cleared;
    }

    private bool IsRowFull(int row)
    {
        for (int col = 0; col < Columns; col++)
        {
            if (_board[row, col] == null)
            {
                return false;
            }
        }

        return true;
    }

    private void RestoreSnapshot(GameSnapshot snapshot)
    {
        Array.Clear(_board);

        for (int row = 0; row < Rows; row++)
        {
            for (int column = 0; column < Columns; column++)
            {
                _board[row, column] = snapshot.Board[row, column];
            }
        }

        _queue.Clear();

        foreach (TetrominoType type in snapshot.Upcoming)
        {
            _queue.Enqueue(type);
        }

        Current = snapshot.Current.Clone();
        CurrentX = snapshot.CurrentX;
        CurrentY = snapshot.CurrentY;
        Score = snapshot.Score;
        Level = snapshot.Level;
        Lines = snapshot.Lines;
        State = snapshot.State;
        _gravityElapsed = snapshot.GravityElapsed;
    }

    private void SpawnPiece()
    {
        EnsureQueue();
        Current = Tetromino.Create(_queue.Dequeue());
        CurrentX = (Columns - Current.Size) / 2;
        CurrentY = 0;
        _gravityElapsed = 0;

        if (!CanPlace(Current, CurrentX, CurrentY))
        {
            State = GameState.GameOver;
        }
    }

    private void EnsureQueue()
    {
        if (_queue.Count == 0)
        {
            RefillQueue();
        }
    }

    /// <summary>7-bag 随机：每七个方块为一个循环，避免长时间出现重复方块。</summary>
    private void RefillQueue()
    {
        var bag = Enum.GetValues<TetrominoType>().ToList();

        for (int i = bag.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (bag[i], bag[j]) = (bag[j], bag[i]);
        }

        foreach (TetrominoType type in bag)
        {
            _queue.Enqueue(type);
        }
    }
}

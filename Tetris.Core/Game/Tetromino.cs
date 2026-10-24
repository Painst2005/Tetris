namespace Tetris.Game;

public enum TetrominoType
{
    I,
    J,
    L,
    O,
    S,
    T,
    Z
}

/// <summary>
/// 一个方块由方阵描述，旋转时整体旋转方阵。
/// </summary>
public sealed class Tetromino
{
    public TetrominoType Type { get; }

    public int[,] Cells { get; }

    public int Size => Cells.GetLength(0);

    private Tetromino(TetrominoType type, int[,] cells)
    {
        Type = type;
        Cells = cells;
    }

    public static Tetromino Create(TetrominoType type)
    {
        return new Tetromino(type, type switch
        {
            TetrominoType.I => new[,]
            {
                { 0, 0, 0, 0 },
                { 1, 1, 1, 1 },
                { 0, 0, 0, 0 },
                { 0, 0, 0, 0 }
            },
            TetrominoType.J => new[,]
            {
                { 1, 0, 0 },
                { 1, 1, 1 },
                { 0, 0, 0 }
            },
            TetrominoType.L => new[,]
            {
                { 0, 0, 1 },
                { 1, 1, 1 },
                { 0, 0, 0 }
            },
            TetrominoType.O => new[,]
            {
                { 1, 1 },
                { 1, 1 }
            },
            TetrominoType.S => new[,]
            {
                { 0, 1, 1 },
                { 1, 1, 0 },
                { 0, 0, 0 }
            },
            TetrominoType.T => new[,]
            {
                { 0, 1, 0 },
                { 1, 1, 1 },
                { 0, 0, 0 }
            },
            TetrominoType.Z => new[,]
            {
                { 1, 1, 0 },
                { 0, 1, 1 },
                { 0, 0, 0 }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        });
    }

    public Tetromino Clone()
    {
        int size = Size;
        int[,] cells = new int[size, size];

        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                cells[row, col] = Cells[row, col];
            }
        }

        return new Tetromino(Type, cells);
    }

    public Tetromino RotateClockwise()
    {
        if (Type == TetrominoType.O)
        {
            return this;
        }

        int size = Size;
        int[,] rotated = new int[size, size];

        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                rotated[col, size - 1 - row] = Cells[row, col];
            }
        }

        return new Tetromino(Type, rotated);
    }

    public Tetromino RotateCounterClockwise()
    {
        if (Type == TetrominoType.O)
        {
            return this;
        }

        int size = Size;
        int[,] rotated = new int[size, size];

        for (int row = 0; row < size; row++)
        {
            for (int col = 0; col < size; col++)
            {
                rotated[size - 1 - col, row] = Cells[row, col];
            }
        }

        return new Tetromino(Type, rotated);
    }
}

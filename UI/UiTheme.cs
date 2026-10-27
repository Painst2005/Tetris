using Microsoft.Win32;
using Tetris.Game;

namespace Tetris.UI;

/// <summary>
/// 界面设计令牌：颜色统一从这里读取，支持深色/浅色主题及平滑过渡。
/// </summary>
internal sealed class UiTheme
{
    public required bool IsDark { get; init; }
    public required Color CanvasTop { get; init; }
    public required Color CanvasBottom { get; init; }
    public required Color GlowPrimary { get; init; }
    public required Color GlowSecondary { get; init; }
    public required Color SurfaceTop { get; init; }
    public required Color SurfaceBottom { get; init; }
    public required Color SurfaceMuted { get; init; }
    public required Color Border { get; init; }
    public required Color BorderStrong { get; init; }
    public required Color TextPrimary { get; init; }
    public required Color TextSecondary { get; init; }
    public required Color TextMuted { get; init; }
    public required Color Accent { get; init; }
    public required Color AccentAlt { get; init; }
    public required Color Success { get; init; }
    public required Color Warning { get; init; }
    public required Color Danger { get; init; }
    public required Color BoardTop { get; init; }
    public required Color BoardBottom { get; init; }
    public required Color BoardBorder { get; init; }
    public required Color GridLine { get; init; }
    public required Color OverlayBackdrop { get; init; }
    public required Color ButtonBackground { get; init; }
    public required Color ButtonHover { get; init; }
    public required Color ButtonPressed { get; init; }
    public required Color ButtonBorder { get; init; }
    public required Color ButtonText { get; init; }
    public required Color Shadow { get; init; }
    public required Color BlockI { get; init; }
    public required Color BlockJ { get; init; }
    public required Color BlockL { get; init; }
    public required Color BlockO { get; init; }
    public required Color BlockS { get; init; }
    public required Color BlockT { get; init; }
    public required Color BlockZ { get; init; }

    public static UiTheme Dark { get; } = new()
    {
        IsDark = true,
        CanvasTop = Color.FromArgb(10, 15, 30),
        CanvasBottom = Color.FromArgb(5, 8, 16),
        GlowPrimary = Color.FromArgb(24, 34, 211, 238),
        GlowSecondary = Color.FromArgb(18, 129, 140, 248),
        SurfaceTop = Color.FromArgb(19, 27, 47),
        SurfaceBottom = Color.FromArgb(12, 18, 33),
        SurfaceMuted = Color.FromArgb(8, 13, 25),
        Border = Color.FromArgb(38, 52, 79),
        BorderStrong = Color.FromArgb(55, 72, 104),
        TextPrimary = Color.FromArgb(242, 246, 252),
        TextSecondary = Color.FromArgb(169, 180, 201),
        TextMuted = Color.FromArgb(112, 125, 149),
        Accent = Color.FromArgb(45, 212, 235),
        AccentAlt = Color.FromArgb(167, 139, 250),
        Success = Color.FromArgb(52, 211, 153),
        Warning = Color.FromArgb(251, 191, 36),
        Danger = Color.FromArgb(248, 113, 113),
        BoardTop = Color.FromArgb(11, 17, 32),
        BoardBottom = Color.FromArgb(6, 10, 19),
        BoardBorder = Color.FromArgb(45, 62, 96),
        GridLine = Color.FromArgb(26, 39, 62),
        OverlayBackdrop = Color.FromArgb(5, 9, 18),
        ButtonBackground = Color.FromArgb(18, 50, 68),
        ButtonHover = Color.FromArgb(23, 70, 92),
        ButtonPressed = Color.FromArgb(14, 38, 51),
        ButtonBorder = Color.FromArgb(47, 112, 138),
        ButtonText = Color.FromArgb(165, 243, 252),
        Shadow = Color.FromArgb(82, 0, 0, 0),
        BlockI = Color.FromArgb(34, 211, 238),
        BlockJ = Color.FromArgb(96, 165, 250),
        BlockL = Color.FromArgb(251, 146, 60),
        BlockO = Color.FromArgb(250, 204, 21),
        BlockS = Color.FromArgb(52, 211, 153),
        BlockT = Color.FromArgb(192, 132, 252),
        BlockZ = Color.FromArgb(248, 113, 113)
    };

    public static UiTheme Light { get; } = new()
    {
        IsDark = false,
        CanvasTop = Color.FromArgb(247, 249, 252),
        CanvasBottom = Color.FromArgb(234, 240, 248),
        GlowPrimary = Color.FromArgb(22, 14, 165, 233),
        GlowSecondary = Color.FromArgb(16, 124, 58, 237),
        SurfaceTop = Color.FromArgb(255, 255, 255),
        SurfaceBottom = Color.FromArgb(246, 249, 253),
        SurfaceMuted = Color.FromArgb(240, 244, 250),
        Border = Color.FromArgb(226, 232, 240),
        BorderStrong = Color.FromArgb(203, 213, 225),
        TextPrimary = Color.FromArgb(15, 23, 42),
        TextSecondary = Color.FromArgb(71, 85, 105),
        TextMuted = Color.FromArgb(133, 146, 166),
        Accent = Color.FromArgb(8, 145, 178),
        AccentAlt = Color.FromArgb(124, 58, 237),
        Success = Color.FromArgb(5, 150, 105),
        Warning = Color.FromArgb(180, 83, 9),
        Danger = Color.FromArgb(220, 38, 38),
        BoardTop = Color.FromArgb(238, 243, 250),
        BoardBottom = Color.FromArgb(250, 252, 255),
        BoardBorder = Color.FromArgb(203, 213, 225),
        GridLine = Color.FromArgb(218, 226, 238),
        OverlayBackdrop = Color.FromArgb(248, 250, 252),
        ButtonBackground = Color.FromArgb(224, 242, 254),
        ButtonHover = Color.FromArgb(186, 230, 253),
        ButtonPressed = Color.FromArgb(125, 211, 252),
        ButtonBorder = Color.FromArgb(125, 211, 252),
        ButtonText = Color.FromArgb(14, 116, 144),
        Shadow = Color.FromArgb(30, 15, 23, 42),
        BlockI = Color.FromArgb(8, 145, 178),
        BlockJ = Color.FromArgb(37, 99, 235),
        BlockL = Color.FromArgb(234, 88, 12),
        BlockO = Color.FromArgb(202, 138, 4),
        BlockS = Color.FromArgb(5, 150, 105),
        BlockT = Color.FromArgb(124, 58, 237),
        BlockZ = Color.FromArgb(220, 38, 38)
    };

    public Color BlockColor(TetrominoType type)
    {
        return type switch
        {
            TetrominoType.I => BlockI,
            TetrominoType.J => BlockJ,
            TetrominoType.L => BlockL,
            TetrominoType.O => BlockO,
            TetrominoType.S => BlockS,
            TetrominoType.T => BlockT,
            TetrominoType.Z => BlockZ,
            _ => TextPrimary
        };
    }

    public static UiTheme Lerp(UiTheme from, UiTheme to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);

        return new UiTheme
        {
            IsDark = t < 0.5f ? from.IsDark : to.IsDark,
            CanvasTop = Blend(from.CanvasTop, to.CanvasTop, t),
            CanvasBottom = Blend(from.CanvasBottom, to.CanvasBottom, t),
            GlowPrimary = Blend(from.GlowPrimary, to.GlowPrimary, t),
            GlowSecondary = Blend(from.GlowSecondary, to.GlowSecondary, t),
            SurfaceTop = Blend(from.SurfaceTop, to.SurfaceTop, t),
            SurfaceBottom = Blend(from.SurfaceBottom, to.SurfaceBottom, t),
            SurfaceMuted = Blend(from.SurfaceMuted, to.SurfaceMuted, t),
            Border = Blend(from.Border, to.Border, t),
            BorderStrong = Blend(from.BorderStrong, to.BorderStrong, t),
            TextPrimary = Blend(from.TextPrimary, to.TextPrimary, t),
            TextSecondary = Blend(from.TextSecondary, to.TextSecondary, t),
            TextMuted = Blend(from.TextMuted, to.TextMuted, t),
            Accent = Blend(from.Accent, to.Accent, t),
            AccentAlt = Blend(from.AccentAlt, to.AccentAlt, t),
            Success = Blend(from.Success, to.Success, t),
            Warning = Blend(from.Warning, to.Warning, t),
            Danger = Blend(from.Danger, to.Danger, t),
            BoardTop = Blend(from.BoardTop, to.BoardTop, t),
            BoardBottom = Blend(from.BoardBottom, to.BoardBottom, t),
            BoardBorder = Blend(from.BoardBorder, to.BoardBorder, t),
            GridLine = Blend(from.GridLine, to.GridLine, t),
            OverlayBackdrop = Blend(from.OverlayBackdrop, to.OverlayBackdrop, t),
            ButtonBackground = Blend(from.ButtonBackground, to.ButtonBackground, t),
            ButtonHover = Blend(from.ButtonHover, to.ButtonHover, t),
            ButtonPressed = Blend(from.ButtonPressed, to.ButtonPressed, t),
            ButtonBorder = Blend(from.ButtonBorder, to.ButtonBorder, t),
            ButtonText = Blend(from.ButtonText, to.ButtonText, t),
            Shadow = Blend(from.Shadow, to.Shadow, t),
            BlockI = Blend(from.BlockI, to.BlockI, t),
            BlockJ = Blend(from.BlockJ, to.BlockJ, t),
            BlockL = Blend(from.BlockL, to.BlockL, t),
            BlockO = Blend(from.BlockO, to.BlockO, t),
            BlockS = Blend(from.BlockS, to.BlockS, t),
            BlockT = Blend(from.BlockT, to.BlockT, t),
            BlockZ = Blend(from.BlockZ, to.BlockZ, t)
        };
    }

    public static Color Blend(Color from, Color to, float t)
    {
        t = Math.Clamp(t, 0f, 1f);

        return Color.FromArgb(
            (int)MathF.Round(from.A + (to.A - from.A) * t),
            (int)MathF.Round(from.R + (to.R - from.R) * t),
            (int)MathF.Round(from.G + (to.G - from.G) * t),
            (int)MathF.Round(from.B + (to.B - from.B) * t));
    }

    public static Color WithAlpha(Color color, int alpha)
    {
        return Color.FromArgb(Math.Clamp(alpha, 0, 255), color);
    }
}

internal static class SystemTheme
{
    private const string PersonalizeKey =
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    /// <summary>读取 Windows 应用主题；读取失败时回退到浅色模式。</summary>
    public static bool IsLightMode()
    {
        try
        {
            object? value = Registry.GetValue(PersonalizeKey, "AppsUseLightTheme", 1);
            return value is not int themeValue || themeValue != 0;
        }
        catch
        {
            return true;
        }
    }
}

namespace UniDesk.Helpers;

public readonly record struct PanelSize(double Width, double Height);

public readonly record struct PanelSizeBounds(
    double MinWidth,
    double MaxWidth,
    double MinHeight,
    double MaxHeight);

public static class PanelSizePolicy
{
    private const double RecommendationWidth = 340;
    private const double WorkAreaWidthMargin = 32;
    private const double WorkAreaHeightMargin = 16;
    private const double HeightRounding = 20;

    public static PanelSize GetRecommendedSize(LogicalRect workArea)
    {
        ValidateWorkArea(workArea);

        var recommendedWidth = Math.Max(1, Math.Min(RecommendationWidth, workArea.Width - WorkAreaWidthMargin));
        var roundedHeight = Math.Round(
            Math.Clamp(workArea.Height * 0.70, Limits.MinPanelHeight, Limits.MaxRecommendedPanelHeight) /
                HeightRounding,
            MidpointRounding.AwayFromZero) * HeightRounding;
        var recommendedHeight = Math.Max(1, Math.Min(roundedHeight, workArea.Height - WorkAreaHeightMargin));
        return new PanelSize(recommendedWidth, recommendedHeight);
    }

    public static PanelSizeBounds GetBounds(LogicalRect workArea)
    {
        ValidateWorkArea(workArea);

        var maxWidth = Math.Max(1, Math.Min(Limits.MaxPanelWidth, workArea.Width - WorkAreaWidthMargin));
        var minWidth = Math.Min(Limits.MinPanelWidth, maxWidth);
        var maxHeight = Math.Max(
            1,
            Math.Min(Limits.MaxPanelHeight, workArea.Height - WorkAreaHeightMargin));
        var minHeight = Math.Min(Limits.MinPanelHeight, maxHeight);
        return new PanelSizeBounds(minWidth, maxWidth, minHeight, maxHeight);
    }

    public static PanelSize ClampActualSize(
        double preferredWidth,
        double preferredHeight,
        LogicalRect workArea)
    {
        var bounds = GetBounds(workArea);
        return new PanelSize(
            ClampFinite(preferredWidth, bounds.MinWidth, bounds.MaxWidth),
            ClampFinite(preferredHeight, bounds.MinHeight, bounds.MaxHeight));
    }

    public static double ClampActualWidth(double preferredWidth, LogicalRect workArea) =>
        ClampActualSize(preferredWidth, Limits.MinPanelHeight, workArea).Width;

    public static double ClampActualHeight(double preferredHeight, LogicalRect workArea) =>
        ClampActualSize(Limits.MinPanelWidth, preferredHeight, workArea).Height;

    public static double ClampPreferredWidth(double width) =>
        ClampFinite(width, Limits.MinPanelWidth, Limits.MaxPanelWidth);

    public static double ClampPreferredHeight(double height) =>
        ClampFinite(height, Limits.MinPanelHeight, Limits.MaxPanelHeight);

    private static double ClampFinite(double value, double minimum, double maximum)
    {
        if (!double.IsFinite(value))
        {
            return minimum;
        }

        return Math.Clamp(value, minimum, maximum);
    }

    private static void ValidateWorkArea(LogicalRect workArea)
    {
        if (!workArea.IsValid)
        {
            throw new ArgumentException("The monitor work area must be finite and non-empty.", nameof(workArea));
        }
    }

    private static class Limits
    {
        public const double MinPanelWidth = 320;
        public const double MaxPanelWidth = 520;
        public const double MinPanelHeight = 560;
        public const double MaxPanelHeight = 1040;
        public const double MaxRecommendedPanelHeight = 840;
    }
}

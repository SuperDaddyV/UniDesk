using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace UniDesk.Controls;

internal sealed class TrayContextMenu : ContextMenu
{
    static TrayContextMenu()
    {
        // Hardcodet 1.1.0 divides cursor coordinates by DPI before assigning offsets.
        // TrayService supplies the original physical point separately. Absolute screen
        // rectangles use pixels; ignore the dependency's converted DIP offsets.
        PlacementProperty.OverrideMetadata(typeof(TrayContextMenu),
            new FrameworkPropertyMetadata(PlacementMode.AbsolutePoint, null, (_, _) => PlacementMode.AbsolutePoint));
        HorizontalOffsetProperty.OverrideMetadata(typeof(TrayContextMenu),
            new FrameworkPropertyMetadata(0d, null, (_, _) => 0d));
        VerticalOffsetProperty.OverrideMetadata(typeof(TrayContextMenu),
            new FrameworkPropertyMetadata(0d, null, (_, _) => 0d));
    }

    internal bool TryPrepareOpening(Point physicalPoint)
    {
        if (IsOpen || !double.IsFinite(physicalPoint.X) || !double.IsFinite(physicalPoint.Y))
            return false;

        PlacementRectangle = new Rect(physicalPoint, new Size());
        return true;
    }
}

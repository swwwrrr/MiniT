using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace MiniT.UI.Controls;

/// <summary>
/// Minimal wrap panel: lays children left-to-right, wrapping to a new row
/// when the current row runs out of horizontal space. Used instead of
/// CommunityToolkit's WrapLayout, whose NuGet packages have had repeated
/// namespace-resolution issues (see CommunityToolkit/Windows#537, #506).
/// </summary>
public sealed class SimpleWrapPanel : Panel
{
    public double HorizontalSpacing { get; set; } = 8;
    public double VerticalSpacing { get; set; } = 8;

    protected override Size MeasureOverride(Size availableSize)
    {
        double rowWidth = 0, rowHeight = 0;
        double totalWidth = 0, totalHeight = 0;

        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            var size = child.DesiredSize;

            if (rowWidth > 0 && rowWidth + HorizontalSpacing + size.Width > availableSize.Width)
            {
                totalWidth = Math.Max(totalWidth, rowWidth);
                totalHeight += rowHeight + VerticalSpacing;
                rowWidth = 0;
                rowHeight = 0;
            }

            rowWidth += (rowWidth > 0 ? HorizontalSpacing : 0) + size.Width;
            rowHeight = Math.Max(rowHeight, size.Height);
        }

        totalWidth = Math.Max(totalWidth, rowWidth);
        totalHeight += rowHeight;

        return new Size(double.IsInfinity(availableSize.Width) ? totalWidth : availableSize.Width, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0, y = 0, rowHeight = 0;

        foreach (var child in Children)
        {
            var size = child.DesiredSize;

            if (x > 0 && x + size.Width > finalSize.Width)
            {
                x = 0;
                y += rowHeight + VerticalSpacing;
                rowHeight = 0;
            }

            child.Arrange(new Rect(x, y, size.Width, size.Height));
            x += size.Width + HorizontalSpacing;
            rowHeight = Math.Max(rowHeight, size.Height);
        }

        return finalSize;
    }
}

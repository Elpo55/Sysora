using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace Sysora.App.Controls;

/// <summary>
/// Lays children out in equal-width columns, as many as fit with at least <see cref="MinItemWidth"/> each.
/// Items of a row share the row's height. Used for cards, so pages adapt from wide monitors to narrow
/// windows without per-page visual states. With <see cref="BalanceRows"/>, cards are spread evenly over the rows and
/// the last row is filled, instead of leaving one card alone under a full row.
/// </summary>
public sealed partial class AdaptiveGridPanel : Panel
{
    public static readonly DependencyProperty MinItemWidthProperty = DependencyProperty.Register(
        nameof(MinItemWidth), typeof(double), typeof(AdaptiveGridPanel), new PropertyMetadata(240d, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ColumnSpacingProperty = DependencyProperty.Register(
        nameof(ColumnSpacing), typeof(double), typeof(AdaptiveGridPanel), new PropertyMetadata(12d, OnLayoutPropertyChanged));

    public static readonly DependencyProperty RowSpacingProperty = DependencyProperty.Register(
        nameof(RowSpacing), typeof(double), typeof(AdaptiveGridPanel), new PropertyMetadata(12d, OnLayoutPropertyChanged));

    public static readonly DependencyProperty MaxColumnsProperty = DependencyProperty.Register(
        nameof(MaxColumns), typeof(int), typeof(AdaptiveGridPanel), new PropertyMetadata(0, OnLayoutPropertyChanged));

    public static readonly DependencyProperty BalanceRowsProperty = DependencyProperty.Register(
        nameof(BalanceRows), typeof(bool), typeof(AdaptiveGridPanel), new PropertyMetadata(false, OnLayoutPropertyChanged));

    /// <summary>Minimum width of an item; determines the number of columns.</summary>
    public double MinItemWidth
    {
        get => (double)GetValue(MinItemWidthProperty);
        set => SetValue(MinItemWidthProperty, value);
    }

    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>Maximum number of columns (0 = no limit).</summary>
    public int MaxColumns
    {
        get => (int)GetValue(MaxColumnsProperty);
        set => SetValue(MaxColumnsProperty, value);
    }

    /// <summary>
    /// Spreads the items evenly over the rows they need and widens the items of a shorter last row to fill it (five cards
    /// that fit four by row become three and two). For cards; leave it off for label and value grids, whose columns must
    /// stay aligned.
    /// </summary>
    public bool BalanceRows
    {
        get => (bool)GetValue(BalanceRowsProperty);
        set => SetValue(BalanceRowsProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var children = VisibleChildren();
        if (children.Count == 0)
        {
            return new Size(0, 0);
        }

        var width = double.IsInfinity(availableSize.Width)
            ? (MinItemWidth * children.Count) + (ColumnSpacing * (children.Count - 1))
            : availableSize.Width;
        var columns = ComputeColumns(width, children.Count);

        double height = 0;
        for (var start = 0; start < children.Count; start += columns)
        {
            var end = Math.Min(start + columns, children.Count);
            var itemWidth = ItemWidth(width, columns, end - start);
            double rowHeight = 0;
            for (var i = start; i < end; i++)
            {
                children[i].Measure(new Size(itemWidth, double.PositiveInfinity));
                rowHeight = Math.Max(rowHeight, children[i].DesiredSize.Height);
            }

            height += rowHeight + (start > 0 ? RowSpacing : 0);
        }

        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var children = VisibleChildren();
        if (children.Count == 0)
        {
            return finalSize;
        }

        var columns = ComputeColumns(finalSize.Width, children.Count);
        double y = 0;
        for (var start = 0; start < children.Count; start += columns)
        {
            var end = Math.Min(start + columns, children.Count);
            var itemWidth = ItemWidth(finalSize.Width, columns, end - start);
            double rowHeight = 0;
            for (var i = start; i < end; i++)
            {
                rowHeight = Math.Max(rowHeight, children[i].DesiredSize.Height);
            }

            for (var i = start; i < end; i++)
            {
                var x = (i - start) * (itemWidth + ColumnSpacing);
                children[i].Arrange(new Rect(x, y, itemWidth, rowHeight));
            }

            y += rowHeight + RowSpacing;
        }

        return finalSize;
    }

    private int ComputeColumns(double width, int count)
    {
        var columns = Math.Max(1, (int)((width + ColumnSpacing) / (Math.Max(MinItemWidth, 1) + ColumnSpacing)));
        columns = Math.Min(columns, count);
        if (MaxColumns > 0)
        {
            columns = Math.Min(columns, MaxColumns);
        }

        if (BalanceRows)
        {
            var rows = (count + columns - 1) / columns;
            columns = (count + rows - 1) / rows;
        }

        return columns;
    }

    /// <summary>Width of each item of a row holding <paramref name="items"/> items.</summary>
    private double ItemWidth(double width, int columns, int items)
    {
        var shared = BalanceRows ? items : columns;
        return Math.Max((width - (ColumnSpacing * (shared - 1))) / shared, 0);
    }

    private List<UIElement> VisibleChildren() =>
        Children.Where(c => c.Visibility == Visibility.Visible).ToList();

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((AdaptiveGridPanel)d).InvalidateMeasure();
}

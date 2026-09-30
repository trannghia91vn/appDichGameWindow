using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GameTranslator.Core;

namespace GameTranslator.App.RegionSelection;

public partial class RegionSelectorWindow : Window
{
    private readonly PhysicalScreenCoordinateMapper coordinateMapper;
    private PhysicalScreenPoint dragStart;
    private bool isDragging;

    public RegionSelectorWindow(PhysicalScreenCoordinateMapper coordinateMapper)
    {
        this.coordinateMapper = coordinateMapper;
        InitializeComponent();

        SourceInitialized += (_, _) => coordinateMapper.CoverVirtualDesktop(this);
        Loaded += (_, _) =>
        {
            UpdateDimmingGeometry(null);
            Activate();
            Focus();
        };
        SizeChanged += (_, _) => UpdateDimmingGeometry(GetCurrentSelectionRect());
    }

    public ScreenRegion? SelectedRegion { get; private set; }

    private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        dragStart = coordinateMapper.GetCursorPosition();
        isDragging = true;
        OverlayCanvas.CaptureMouse();
        UpdateSelection(dragStart);
        e.Handled = true;
    }

    private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!isDragging)
        {
            return;
        }

        UpdateSelection(coordinateMapper.GetCursorPosition());
    }

    private void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!isDragging)
        {
            return;
        }

        var dragEnd = coordinateMapper.GetCursorPosition();
        isDragging = false;
        OverlayCanvas.ReleaseMouseCapture();

        var x = Math.Min(dragStart.X, dragEnd.X);
        var y = Math.Min(dragStart.Y, dragEnd.Y);
        var width = Math.Abs(dragEnd.X - dragStart.X);
        var height = Math.Abs(dragEnd.Y - dragStart.Y);

        if (width == 0 || height == 0)
        {
            SelectionRectangle.Visibility = Visibility.Collapsed;
            SizeLabel.Visibility = Visibility.Collapsed;
            UpdateDimmingGeometry(null);
            return;
        }

        SelectedRegion = new ScreenRegion(x, y, width, height);
        DialogResult = true;
        e.Handled = true;
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        SelectedRegion = null;
        DialogResult = false;
        e.Handled = true;
    }

    private void UpdateSelection(PhysicalScreenPoint dragEnd)
    {
        var startDip = coordinateMapper.ToWindowDip(this, dragStart);
        var endDip = coordinateMapper.ToWindowDip(this, dragEnd);
        var selectionRect = Normalize(startDip, endDip);

        Canvas.SetLeft(SelectionRectangle, selectionRect.X);
        Canvas.SetTop(SelectionRectangle, selectionRect.Y);
        SelectionRectangle.Width = selectionRect.Width;
        SelectionRectangle.Height = selectionRect.Height;
        SelectionRectangle.Visibility = Visibility.Visible;

        var physicalWidth = Math.Abs(dragEnd.X - dragStart.X);
        var physicalHeight = Math.Abs(dragEnd.Y - dragStart.Y);
        SizeText.Text = $"{physicalWidth} × {physicalHeight} px";
        SizeLabel.Visibility = Visibility.Visible;
        SizeLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

        var labelLeft = Math.Min(
            selectionRect.Right + 8,
            Math.Max(8, OverlayCanvas.ActualWidth - SizeLabel.DesiredSize.Width - 8));
        var labelTop = Math.Min(
            selectionRect.Bottom + 8,
            Math.Max(8, OverlayCanvas.ActualHeight - SizeLabel.DesiredSize.Height - 8));
        Canvas.SetLeft(SizeLabel, labelLeft);
        Canvas.SetTop(SizeLabel, labelTop);

        UpdateDimmingGeometry(selectionRect);
    }

    private Rect? GetCurrentSelectionRect()
    {
        if (SelectionRectangle.Visibility != Visibility.Visible)
        {
            return null;
        }

        return new Rect(
            Canvas.GetLeft(SelectionRectangle),
            Canvas.GetTop(SelectionRectangle),
            SelectionRectangle.Width,
            SelectionRectangle.Height);
    }

    private void UpdateDimmingGeometry(Rect? selectionRect)
    {
        var geometry = new GeometryGroup { FillRule = FillRule.EvenOdd };
        geometry.Children.Add(new RectangleGeometry(
            new Rect(0, 0, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight)));

        if (selectionRect is { Width: > 0, Height: > 0 } selected)
        {
            geometry.Children.Add(new RectangleGeometry(selected));
        }

        DimmingPath.Data = geometry;
    }

    private static Rect Normalize(Point first, Point second) => new(
        Math.Min(first.X, second.X),
        Math.Min(first.Y, second.Y),
        Math.Abs(second.X - first.X),
        Math.Abs(second.Y - first.Y));
}

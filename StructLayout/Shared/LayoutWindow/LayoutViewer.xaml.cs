using Microsoft.VisualStudio.Shell;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace StructLayout
{

    public static class DrawingHalpers
    {
        public static void DrawPolygon(this DrawingContext drawingContext, Brush brush, Pen pen, Point[] points, FillRule fill_rule)
        {
            DrawPolygonOrPolyline(drawingContext, brush, pen, points, fill_rule, true);
        }
        public static void DrawPolyline(this DrawingContext drawingContext, Brush brush, Pen pen, Point[] points, FillRule fill_rule)
        {
            DrawPolygonOrPolyline(drawingContext, brush, pen, points, fill_rule, false);
        }

        private static void DrawPolygonOrPolyline(DrawingContext drawingContext, Brush brush, Pen pen, Point[] points, FillRule fill_rule, bool isClosed)
        {
            StreamGeometry geo = new StreamGeometry();
            geo.FillRule = fill_rule;

            // Open the context to use for drawing.
            using (StreamGeometryContext context = geo.Open())
            {
                context.BeginFigure(points[0], true, isClosed);
                context.PolyLineTo(points.Skip(1).ToArray(), true, false);
            }
            drawingContext.DrawGeometry(brush, pen, geo);
        }
    };


    public class Mouse2DScrollEventArgs
    {
        public Mouse2DScrollEventArgs(Vector delta) { Delta = delta; }

        public Vector Delta { get; }
    };

    public delegate void Mouse2DScrollEventHandler(object sender, Mouse2DScrollEventArgs e);

    public class CustomScrollViewer : ScrollViewer
    {
        private bool Is2DScolling { set; get; }
        private Point lastScrollingPosition { set; get; }

        public event MouseWheelEventHandler OnControlMouseWheel;
        public event Mouse2DScrollEventHandler On2DMouseScroll;

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            {
                if (OnControlMouseWheel != null)
                {
                    OnControlMouseWheel.Invoke(this, e);
                }
            }
            else
            {
                //Default behavior
                base.OnMouseWheel(e);
            }
        }

        protected override void OnMouseEnter(MouseEventArgs e)
        {
            if (Mouse.MiddleButton == MouseButtonState.Pressed)
            {
                Is2DScolling = true;
                lastScrollingPosition = e.GetPosition(this);
            }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            Is2DScolling = false;
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
            {
                Is2DScolling = true;
                lastScrollingPosition = e.GetPosition(this);
            }
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Released)
            {
                Is2DScolling = false;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (Is2DScolling)
            {
                Point nextPosition = e.GetPosition(this);
                if (On2DMouseScroll != null)
                {
                    On2DMouseScroll.Invoke(this, new Mouse2DScrollEventArgs(nextPosition - lastScrollingPosition));
                }
                lastScrollingPosition = nextPosition;
            }
            base.OnMouseMove(e);
        }
    }

    public class VisualHost : UIElement
    {
        public DrawingVisual Visual = new DrawingVisual();

        public VisualHost()
        {
            IsHitTestVisible = false;
        }

        protected override int VisualChildrenCount
        {
            get { return Visual != null ? 1 : 0; }
        }

        protected override Visual GetVisualChild(int index)
        {
            return Visual;
        }
    }


    public partial class LayoutViewer : UserControl
    {
        public enum DisplayAlignmentType
        {
            Struct = 0, 
            Cacheline, 
            Custom
        }

        public enum DisplayMode
        {
             Stack = 0, 
             Flat
        }

        public enum GridBase
        {
            Decimal = 0,
            Hexadecimal,
        }

        const uint CACHELINE_SIZE = 64;

        const double BaseNodeWidth  = 25;
        const double BaseNodeHeight = 25;

        const double textRenderMinWidth = 15;
        const double textRenderMinHeight = 15;

        const double MarginLeft   = 50;
        const double MarginRight  = 50;
        const double MarginTop    = 25;
        const double MarginBottom = 25;
        const double paddingSize  = 5;

        public static DisplayAlignmentType DefaultDisplayAlignment { set; get; } = DisplayAlignmentType.Struct;
        public static uint DefaultDisplayCustomAlignment { set; get; } = 8;
        public static DisplayMode DefaultDisplayMode { set; get; } = DisplayMode.Stack;
        public static GridBase DefaultGridNumberBase { set; get; } = GridBase.Hexadecimal;

        private Typeface Font = new Typeface("Verdana");
        private Pen gridPen   = new Pen(new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)), 1);

        private double NodeWidth   = BaseNodeWidth;
        private double NodeHeight  = BaseNodeHeight;
        private uint   MaxPaddingH = 0;
        private double MaxPaddingV = 0;

        private LayoutNode Root { set; get; }
        private LayoutNode Hover { set; get; }

        private ToolTip tooltip = new ToolTip() { Content = new LayoutNodeTooltip(), Padding = new Thickness(0) };
        private DispatcherTimer tooltipTimer = new DispatcherTimer() { Interval = new TimeSpan(4000000) };

        private GridBase GridNumberBase { set; get; } = GridBase.Hexadecimal;

        private Pen nodeBorderPen = new Pen(Colors.GetCategoryForeground(), 2);
        private Brush overlayBrush = Brushes.White.Clone(); 

        private VisualHost baseVisual    = new VisualHost();
        private VisualHost gridVisual    = new VisualHost();
        private VisualHost textVisual    = new VisualHost();
        private VisualHost overlayVisual = new VisualHost();

        private uint DisplayGridColumns = 4;
        private uint DisplayGridRows    = 0;

        public LayoutViewer()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            InitializeComponent();
            this.DataContext = this;

            overlayBrush.Opacity = 0.3;
            SetDisplayGridColumns(DefaultDisplayCustomAlignment);
            GridNumberBase = DefaultGridNumberBase;

            tooltipTimer.Tick += ShowTooltip;

            displayAlignementComboBox.ItemsSource = Enum.GetValues(typeof(DisplayAlignmentType)).Cast<DisplayAlignmentType>();
            displayAlignementComboBox.SelectedIndex = (int)DefaultDisplayAlignment;

            displayModeComboBox.ItemsSource = Enum.GetValues(typeof(DisplayMode)).Cast<DisplayMode>();
            displayModeComboBox.SelectedIndex = (int)DefaultDisplayMode;

            scrollViewer.Loaded += ScrollViewer_OnLoaded;
            scrollViewer.On2DMouseScroll += ScrollViewer_On2DMouseScroll;
            scrollViewer.MouseMove += ScrollViewer_OnMouseMove;
            scrollViewer.MouseLeave += ScrollViewer_OnMouseLeave;
            scrollViewer.MouseLeftButtonUp += ScrollViewer_OnClick;
            scrollViewer.MouseRightButtonUp += ScrollViewer_OnMouseContext;

            displayAlignmentValue.TextChanged += DisplayAlign_Changed;
        }

        public void SetLayout(LayoutNode root)
        {
            Root = root;
            SetHoverNode(null);

            uint displayAlignment = Root != null && GetSelectedDisplayAlignment() == DisplayAlignmentType.Struct? Root.Align : DisplayGridColumns;

            if (!SetDisplayGridColumns(displayAlignment))
            {
                RefreshNodeRenderData();
                SetupCanvas();
                RenderGrid();
                RefreshShapes();
            }
        }

        public void RefreshDefaults()
        {
            if (DefaultGridNumberBase != GridNumberBase)
            {
                GridNumberBase = DefaultGridNumberBase;

                RenderGrid();
                RefreshShapes();
            }
        }

        private DisplayAlignmentType GetSelectedDisplayAlignment()
        {
            return displayAlignementComboBox.SelectedItem != null? (DisplayAlignmentType)displayAlignementComboBox.SelectedItem : DisplayAlignmentType.Struct;
        }

        public DisplayMode GetSelectedDisplayMode()
        {
            return displayModeComboBox.SelectedItem != null ? (DisplayMode)displayModeComboBox.SelectedItem : DisplayMode.Stack;
        }

        private void ShowTooltip(Object a, object b)
        {
            tooltipTimer.Stop();
            (tooltip.Content as LayoutNodeTooltip).ReferenceNode = Hover;
            tooltip.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
            tooltip.IsOpen = true;
            tooltip.PlacementTarget = this;
        }

        private void SetHoverNode(LayoutNode node)
        {
            if (node != Hover)
            {
                //Close Tooltip 
                tooltip.IsOpen = false;
                tooltipTimer.Stop();
                
                Hover = node;

                //Start Tooltip if applicable
                if (Hover != null)
                {
                    tooltipTimer.Start();
                }

                RenderOverlay();
            }
        }

        private void PrepareRenderDataStack(LayoutNode node)
        {
            //Padding
            uint endOffsetParent = node.Parent.Offset + node.Parent.Size;
            uint parentLastOffset = endOffsetParent - 1;
            uint parentStartRow = GetRow(node.Parent.Offset);
            uint parentStartCol = GetCol(node.Parent.Offset);
            uint parentLastRow = GetRow(parentLastOffset);
            uint parentEndCol = GetCol(endOffsetParent);

            uint endOffset = node.Offset + node.Size;
            uint lastOffset = endOffset - 1;
            uint startCol = GetCol(node.Offset);
            uint startRow = GetRow(node.Offset);
            uint lastRow = GetRow(lastOffset);
            uint endCol = GetCol(endOffset);
            uint endRow = GetRow(endOffset);

            //real padding 
            var paddings = node.RenderData.Paddings;
            var parentPaddings = node.Parent.RenderData.Paddings;

            paddings[(int)RenderData.PaddingSide.OuterLeft] = 1 + parentPaddings[(int)RenderData.PaddingSide.OuterLeft];
            paddings[(int)RenderData.PaddingSide.OuterRight] = 1 + parentPaddings[(int)RenderData.PaddingSide.OuterRight];

            paddings[(int)RenderData.PaddingSide.InnerLeft] = 1 + (startCol == 0 ? parentPaddings[(int)RenderData.PaddingSide.OuterLeft] : (node.Offset == node.Parent.Offset ? parentPaddings[(int)RenderData.PaddingSide.InnerLeft] : 0));
            paddings[(int)RenderData.PaddingSide.InnerRight] = 1 + (endCol == 0 ? parentPaddings[(int)RenderData.PaddingSide.OuterRight] : (endOffset == endOffsetParent ? parentPaddings[(int)RenderData.PaddingSide.InnerRight] : 0));

            paddings[(int)RenderData.PaddingSide.OuterTop] = 1 + (startRow == parentStartRow ? parentPaddings[(int)RenderData.PaddingSide.OuterTop] : ((parentStartCol != 0 && node.Offset <= node.Parent.Offset + DisplayGridColumns) ? parentPaddings[(int)RenderData.PaddingSide.InnerTop] : 0));
            paddings[(int)RenderData.PaddingSide.InnerTop] = 1 + ((parentStartCol != 0 && startRow == parentStartRow) ? parentPaddings[(int)RenderData.PaddingSide.InnerTop] : 0);

            paddings[(int)RenderData.PaddingSide.OuterBottom] = 1 + (lastRow == parentLastRow ? parentPaddings[(int)RenderData.PaddingSide.OuterBottom] : ((parentEndCol != 0 && lastOffset >= parentLastOffset - DisplayGridColumns) ? parentPaddings[(int)RenderData.PaddingSide.InnerBottom] : 0));
            paddings[(int)RenderData.PaddingSide.InnerBottom] = 1 + ((parentEndCol != 0 && lastRow == parentLastRow) ? parentPaddings[(int)RenderData.PaddingSide.InnerBottom] : 0);

            //Compute max indentation

            uint thisPaddingH = 0;
            uint thisPaddingV = 0;

            if (startRow == lastRow)
            {
                thisPaddingH = node.Size == 1 ? paddings[(int)RenderData.PaddingSide.InnerLeft] + paddings[(int)RenderData.PaddingSide.InnerRight] : Math.Max(paddings[(int)RenderData.PaddingSide.InnerLeft], paddings[(int)RenderData.PaddingSide.InnerRight]);
                thisPaddingV = paddings[(int)RenderData.PaddingSide.OuterTop] + paddings[(int)RenderData.PaddingSide.OuterBottom];
            }
            else
            {
                uint maxPaddingHTop = (startCol + 1) == DisplayGridColumns ? paddings[(int)RenderData.PaddingSide.InnerLeft] + paddings[(int)RenderData.PaddingSide.OuterRight] : Math.Max(paddings[(int)RenderData.PaddingSide.InnerLeft], paddings[(int)RenderData.PaddingSide.OuterRight]);
                uint maxPaddingHBottom = startCol == 0 ? paddings[(int)RenderData.PaddingSide.OuterLeft] + paddings[(int)RenderData.PaddingSide.InnerRight] : Math.Max(paddings[(int)RenderData.PaddingSide.OuterLeft], paddings[(int)RenderData.PaddingSide.InnerRight]);
                thisPaddingH = Math.Max(maxPaddingHTop, maxPaddingHBottom);

                uint OTIB = endCol != 0 && endRow == startRow + 1 ? paddings[(int)RenderData.PaddingSide.OuterTop] + paddings[(int)RenderData.PaddingSide.InnerBottom] : Math.Max(paddings[(int)RenderData.PaddingSide.OuterTop], paddings[(int)RenderData.PaddingSide.InnerBottom]);
                uint ITOB = startCol != 0 && lastRow <= startRow + 1 ? paddings[(int)RenderData.PaddingSide.InnerTop] + paddings[(int)RenderData.PaddingSide.OuterBottom] : Math.Max(paddings[(int)RenderData.PaddingSide.InnerTop], paddings[(int)RenderData.PaddingSide.OuterBottom]);
                uint ITIB = endCol != 0 && startCol != 0 && node.Size < 2 * DisplayGridColumns && lastRow == startRow + 2 ? paddings[(int)RenderData.PaddingSide.InnerTop] + paddings[(int)RenderData.PaddingSide.InnerBottom] : Math.Max(paddings[(int)RenderData.PaddingSide.InnerTop], paddings[(int)RenderData.PaddingSide.InnerBottom]);
                thisPaddingV = Math.Max(Math.Max(OTIB, ITOB), ITIB);
            }

            MaxPaddingH = Math.Max(MaxPaddingH, thisPaddingH);
            MaxPaddingV = Math.Max(MaxPaddingV, thisPaddingV);

            foreach (LayoutNode child in node.Children)
            {
                PrepareRenderDataStack(child);
            }
        }

        private void PrepareRenderDataFlat(LayoutNode node)
        {
            var paddings = node.RenderData.Paddings;
            foreach (RenderData.PaddingSide side in (RenderData.PaddingSide[])Enum.GetValues(typeof(RenderData.PaddingSide)))
            {
                paddings[(int)side] = 0;
            }

            foreach (LayoutNode child in node.Children)
            {
                PrepareRenderDataFlat(child);
            }         
        } 

        private void ComputeRenderData(LayoutNode node)
        {
            ComputeRenderShape(node);

            foreach (LayoutNode child in node.Children)
            {
                ComputeRenderData(child);
            }           
        }

        private string GetNodeLabel(LayoutNode node)
        {
            return node.Name.Length > 0 ? node.Name : ( node.Type.Length > 0? node.Type : node.Category.ToString() );
        }

        private void ComputeTextLabelImpl(LayoutNode node, double posX, double posY, double sizeX, double sizeY)
        {
            if (sizeX >= textRenderMinWidth && sizeY >= textRenderMinHeight)
            {
                node.RenderData.Text = new FormattedText(GetNodeLabel(node), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 12, Colors.GetCategoryForeground(), VisualTreeHelper.GetDpi(this).PixelsPerDip);
                node.RenderData.Text.MaxTextWidth = Math.Min(sizeX, node.RenderData.Text.Width);
                node.RenderData.Text.MaxTextHeight = NodeHeight;
                node.RenderData.TextPosition = new Point(posX + (sizeX - node.RenderData.Text.Width) * 0.5, posY + (sizeY - node.RenderData.Text.Height) * 0.5);
            }
        }

        private void ComputeTextLabel(LayoutNode node)
        {
            Point[] p = node.RenderData.Points;
            node.RenderData.Text = null;

            switch (node.RenderData.Category)
            {
                case RenderData.ShapeCategory.Simple:
                    ComputeTextLabelImpl(node,p[0].X, p[0].Y, p[1].X, p[1].Y);
                    break;

                case RenderData.ShapeCategory.Split:
                    double sizeA = p[3].X - p[1].X;
                    double sizeB = p[2].X - p[0].X;

                    if (sizeA >= sizeB)
                    {
                        ComputeTextLabelImpl(node, p[1].X, p[0].Y, sizeA, p[4].X);
                    }
                    else
                    {
                        ComputeTextLabelImpl(node, p[0].X, p[2].Y, sizeB, p[4].Y);
                    }
                    break;

                case RenderData.ShapeCategory.Blob:
                    uint startRow = GetRow(node.Offset);
                    uint lastRow = GetRow(node.Offset + node.Size - 1);
                    
                    uint startCol = GetCol(node.Offset);
                    uint endCol = GetCol(node.Offset + node.Size);

                    if (startRow +1 == lastRow && startCol != 0 && endCol != 0)
                    {
                        //no full line
                        if (DisplayGridColumns - startCol >= endCol)
                        {
                            ComputeTextLabelImpl(node, p[0].X, p[0].Y, p[2].X - p[0].X, p[2].Y - p[0].Y);
                        }
                        else
                        {
                            ComputeTextLabelImpl(node, p[6].X, p[6].Y, p[4].X - p[6].X, p[4].Y - p[6].Y);
                        }
                    }
                    else
                    {
                        //center to the lines that take the whole alignment
                        double startY = startCol == 0? p[0].Y : p[6].Y;
                        double endY   = endCol   == 0? p[5].Y : p[2].Y;

                        ComputeTextLabelImpl(node, p[5].X, startY, p[1].X-p[5].X, endY-startY);
                    }
                    break;
            }
        }

        private void ComputeRenderShape(LayoutNode node)
        {
            uint offset = node.Offset;
            uint size = node.Size;

            uint lastOffset = (offset + size) - 1;
            uint startRow = GetRow(offset);
            uint startCol = GetCol(offset);
            uint lastRow = GetRow(lastOffset);
            uint lastCol = GetCol(lastOffset);

            double paddingIR = GetPadding(node, RenderData.PaddingSide.InnerRight);
            double paddingIL = GetPadding(node, RenderData.PaddingSide.InnerLeft);
            double paddingOT = GetPadding(node, RenderData.PaddingSide.OuterTop);
            double paddingOB = GetPadding(node, RenderData.PaddingSide.OuterBottom);

            if (startRow == lastRow)
            {
                node.RenderData.Category = RenderData.ShapeCategory.Simple;
                node.RenderData.Points = new Point[2] { 
                    new Point { X = MarginLeft + paddingIL + (startCol * NodeWidth), Y = MarginTop + paddingOT + (startRow * NodeHeight) }, //offset
                    new Point { X = size * NodeWidth - (paddingIL + paddingIR), Y = NodeHeight - (paddingOT + paddingOB) } //size
                };
            }
            else
            {
                double paddingOL = GetPadding(node, RenderData.PaddingSide.OuterLeft);
                double paddingOR = GetPadding(node, RenderData.PaddingSide.OuterRight);
                double paddingIT = GetPadding(node, RenderData.PaddingSide.InnerTop);
                double paddingIB = GetPadding(node, RenderData.PaddingSide.InnerBottom);

                if (size <= DisplayGridColumns)
                {
                    node.RenderData.Category = RenderData.ShapeCategory.Split;
                    node.RenderData.Points = new Point[5] {
                        new Point { X = MarginLeft + paddingOL,                                    Y = MarginTop + paddingOT + (startRow * NodeHeight) }, 
                        new Point { X = MarginLeft + paddingIL + (startCol * NodeWidth),           Y = MarginTop - paddingIB + ((startRow + 1) * NodeHeight) }, 
                        new Point { X = MarginLeft - paddingIR + ((lastCol + 1) * NodeWidth),      Y = MarginTop + paddingIT + ((startRow + 1) * NodeHeight) }, 
                        new Point { X = MarginLeft - paddingOR + (DisplayGridColumns * NodeWidth), Y = MarginTop - paddingOB + ((startRow + 2) * NodeHeight) }, 
                        new Point { X = NodeHeight - (paddingOT + paddingIB),                      Y = NodeHeight - (paddingIT + paddingOB) }, //size
                    };
                }
                else
                {
                    node.RenderData.Category = RenderData.ShapeCategory.Blob;

                    double x0 = MarginLeft + paddingOL;
                    double x1 = MarginLeft + paddingIL + (startCol * NodeWidth);
                    double x2 = MarginLeft - paddingIR + ((lastCol + 1) * NodeWidth);
                    double x3 = MarginLeft - paddingOR + (DisplayGridColumns * NodeWidth);

                    double y0 = MarginTop + paddingOT + (startRow * NodeHeight);
                    double y1 = MarginTop + paddingIT + ((startRow + 1) * NodeHeight);
                    double y2 = MarginTop - paddingIB + (lastRow * NodeHeight);
                    double y3 = MarginTop - paddingOB + ((lastRow + 1) * NodeHeight);

                    //preallocate this memory in the node itself
                    node.RenderData.Points = new Point[8] {
                        new Point { X = x1, Y = y0 },
                        new Point { X = x3, Y = y0 },
                        new Point { X = x3, Y = y2 },
                        new Point { X = x2, Y = y2 },
                        new Point { X = x2, Y = y3 },
                        new Point { X = x0, Y = y3 },
                        new Point { X = x0, Y = y1 },
                        new Point { X = x1, Y = y1 },
                        };
                }
            }

            ComputeTextLabel(node);
        } 
            
        private void RefreshNodeSize()
        {
            if (Root != null)
            {
                NodeWidth = BaseNodeWidth + (paddingSize * MaxPaddingH);
                NodeHeight = BaseNodeHeight + (paddingSize * MaxPaddingV);

                ComputeRenderData(Root);
            }
        }

        private void RefreshNodeRenderData()
        {
            if (Root != null)
            {
                MaxPaddingH = 0;
                MaxPaddingV = 0;

                switch (GetSelectedDisplayMode())
                {
                    case DisplayMode.Stack:
                        {
                            foreach (LayoutNode child in Root.Children)
                            {
                                PrepareRenderDataStack(child);
                            }
                        }
                        break;
                    case DisplayMode.Flat:
                        {
                            PrepareRenderDataFlat(Root);
                        }
                        break;
                }

                RefreshNodeSize();
            } 
        }

        private bool SetDisplayGridColumns(uint input)
        {
            bool ret = false;

            if (DisplayGridColumns != input)
            {
                DisplayGridColumns = Math.Max(1,input);
                RefreshNodeRenderData();

                SetupCanvas();
                RenderGrid();
                RefreshShapes();

                ret = true;
            }

            if (displayAlignmentValue != null && input.ToString() != displayAlignmentValue.Text)
            {
                displayAlignmentValue.Text = input.ToString();
            }

            return ret;
        }

        private uint GetRow(uint offset) { return offset / DisplayGridColumns; }
        private uint GetCol(uint offset) { return offset % DisplayGridColumns; }

        private uint GetOffset(uint row, uint col) { return row*DisplayGridColumns+col; }

        private void SetupCanvas()
        {  
            DisplayGridRows = Root == null? 0 : GetRow(Root.Size - 1) + 1;
            canvas.Width  = MarginLeft + MarginRight + (DisplayGridColumns * NodeWidth);
            canvas.Height = MarginTop + MarginBottom + (DisplayGridRows * NodeHeight);
        }

        private void RefreshShapes()
        {
            RenderBase();
            RefreshCanvasVisual(gridVisual);
            RenderLabels();
            RenderOverlay();
        }

        private void RenderNodeStack(DrawingContext drawingContext, LayoutNode node)
        {
            RenderBasicShape(drawingContext, node, node.RenderData.Background);

            if (node.IsExpanded)
            {
                if (node.ExpansionIndex.HasValue)
                {
                    RenderNodeStack(drawingContext, node.Children[node.ExpansionIndex.Value]);
                }
                else
                {
                    foreach (LayoutNode child in node.Children)
                    {
                        RenderNodeStack(drawingContext, child);
                    }
                }
            }
        }

        private void RenderNodeFlat(DrawingContext drawingContext, LayoutNode node)
        {
            if (node.IsExpanded)
            {
                if (node.ExpansionIndex.HasValue)
                {
                    RenderNodeFlat(drawingContext, node.Children[node.ExpansionIndex.Value]);
                }
                else 
                {
                    foreach (LayoutNode child in node.Children)
                    {
                        RenderNodeFlat(drawingContext, child);
                    }
                }
            }
            else
            {
                RenderBasicShape(drawingContext, node, node.RenderData.Background);
            }
        }

        private void RenderGridShape(DrawingContext drawingContext)
        {
            if (Root == null)
            {
                return;
            }

            uint numCols = DisplayGridColumns;
            uint numRows = DisplayGridRows;

            for (uint c = 0; c <= numCols;++c)
            {
                double h = MarginLeft + (c * NodeWidth);
                drawingContext.DrawLine(gridPen, new Point(h, 0), new Point(h,canvas.Height-MarginBottom));
            }

            for (uint r = 0; r <= numRows;++r)
            {
                double h = MarginTop+(r*NodeHeight);
                drawingContext.DrawLine(gridPen, new Point(0, h), new Point(canvas.Width-MarginRight, h));
            }

            //Draw Labels
            for (uint c = 0; c < numCols; ++c)
            {
                var label = GridNumberBase == GridBase.Hexadecimal? c.ToString("X") : c.ToString();
                var txt = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 12, scrollViewer.Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                drawingContext.DrawText(txt, new Point(MarginLeft + (c * NodeWidth) + (NodeWidth - txt.Width) * 0.5, (MarginTop - txt.Height) * 0.5));
            }

            //Draw Labels
            for (uint r = 0; r < numRows; ++r)
            {
                uint val = r * DisplayGridColumns;
                var label = GridNumberBase == GridBase.Hexadecimal? "0x" + val.ToString("X") : val.ToString();
                var txt = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 12, scrollViewer.Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
                drawingContext.DrawText(txt, new Point((MarginLeft - txt.Width) * 0.5, MarginTop + (r * NodeHeight) + (NodeHeight - txt.Height) * 0.5));
            }
        }

        private double GetPadding(LayoutNode node, RenderData.PaddingSide flag)
        {
            return node.RenderData.Paddings[(int)flag] * paddingSize;
        }

        private void RenderBasicShape(DrawingContext drawingContext, LayoutNode node, Brush color)
        {
            Point[] p = node.RenderData.Points;

            switch (node.RenderData.Category)
            {
                case RenderData.ShapeCategory.Simple:
                    drawingContext.DrawRectangle(color, nodeBorderPen, new Rect(p[0].X, p[0].Y, p[1].X, p[1].Y));
                    break;
                case RenderData.ShapeCategory.Split:
                    drawingContext.DrawRectangle(color, null, new Rect(p[1].X, p[0].Y, p[3].X - p[1].X, p[4].X));
                    drawingContext.DrawRectangle(color, null, new Rect(p[0].X, p[2].Y, p[2].X - p[0].X, p[4].Y));

                    drawingContext.DrawLine(nodeBorderPen, new Point(p[3].X, p[0].Y), new Point(p[1].X, p[0].Y));
                    drawingContext.DrawLine(nodeBorderPen, new Point(p[3].X, p[1].Y), new Point(p[1].X, p[1].Y));
                    drawingContext.DrawLine(nodeBorderPen, new Point(p[1].X, p[0].Y), new Point(p[1].X, p[1].Y));

                    drawingContext.DrawLine(nodeBorderPen, new Point(p[0].X, p[2].Y), new Point(p[2].X, p[2].Y));
                    drawingContext.DrawLine(nodeBorderPen, new Point(p[0].X, p[3].Y), new Point(p[2].X, p[3].Y));
                    drawingContext.DrawLine(nodeBorderPen, new Point(p[2].X, p[2].Y), new Point(p[2].X, p[3].Y));

                    break;
                case RenderData.ShapeCategory.Blob:
                    DrawingHalpers.DrawPolygon(drawingContext, color, nodeBorderPen, p, FillRule.Nonzero);
                    break;
            }
        }

        private void RenderLabelSimple(DrawingContext drawingContext, LayoutNode node)
        {
            if (node.RenderData.Text != null && node.RenderData.TextPosition != null)
            {
                drawingContext.DrawText(node.RenderData.Text, node.RenderData.TextPosition);
            }
        }

        private void RenderLabel(DrawingContext drawingContext, LayoutNode node)
        {
            if (node.IsExpanded)
            {
                if (node.ExpansionIndex.HasValue)
                {
                    RenderLabel(drawingContext, node.Children[node.ExpansionIndex.Value]);
                }
                else
                {
                    foreach (LayoutNode child in node.Children)
                    {
                        RenderLabel(drawingContext, child);
                    }
                }
            }
            else 
            {
                RenderLabelSimple(drawingContext, node);
            }
        }

        private void RenderBase()
        {
            using (DrawingContext drawingContext = baseVisual.Visual.RenderOpen())
            {
                if (Root != null)
                {
                    switch (GetSelectedDisplayMode())
                    {
                        case DisplayMode.Stack: RenderNodeStack(drawingContext, Root); break;
                        case DisplayMode.Flat:  RenderNodeFlat(drawingContext, Root); break;
                    }
                }
            }

            //force a canvas redraw
            RefreshCanvasVisual(baseVisual);
        }

        private void RenderLabels()
        {
            using (DrawingContext drawingContext = textVisual.Visual.RenderOpen())
            {
                if (Root != null)
                {
                    RenderLabel(drawingContext, Root);
                }
            }

            //force a canvas redraw
            RefreshCanvasVisual(textVisual);
        }

        private void RenderGrid()
        {
            using (DrawingContext drawingContext = gridVisual.Visual.RenderOpen())
            {
                RenderGridShape(drawingContext); 
            }
            RefreshCanvasVisual(gridVisual);
        }

        private void RenderOverlay()
        {
            using (DrawingContext drawingContext = overlayVisual.Visual.RenderOpen())
            {
                if (Hover != null)
                {
                    RenderBasicShape(drawingContext, Hover, overlayBrush);
                }
            }

            RefreshCanvasVisual(overlayVisual);
        }

        private void RefreshCanvasVisual(VisualHost visual)
        {
            canvas.Children.Remove(visual);
            canvas.Children.Add(visual);
        }

        private void ExpandAllNodes(LayoutNode node)
        {
            if (node.Children.Count > 0)
            {                 
                node.Expand();

                if (node.IsExpanded)
                {
                    if (node.ExpansionIndex.HasValue)
                    {
                        ExpandAllNodes(node.Children[node.ExpansionIndex.Value]); 
                    }
                    else
                    {
                        foreach (LayoutNode child in node.Children)
                        {
                            ExpandAllNodes(child);
                        }
                    }
                }
            }
        }

        private bool CollapseNode(LayoutNode node)
        {
            if (node.IsExpanded)
            {
                node.Collapse();
                foreach(LayoutNode child in node.Children)
                {
                    CollapseNode(child);
                }
                return true;
            }
            return false;
        }

        private bool IsPointInside(RenderData renderData, Point localPos)
        {
            Point[] p = renderData.Points;
            switch (renderData.Category)
            {
                case RenderData.ShapeCategory.Simple:
                    return localPos.X >= p[0].X && localPos.X <= (p[0].X+p[1].X) && localPos.Y >= p[0].Y && localPos.Y <= (p[0].Y+p[1].Y); 

                case RenderData.ShapeCategory.Split:
                    return (localPos.X >= p[1].X && localPos.X <= p[3].X && localPos.Y >= p[0].Y && localPos.Y <= (p[0].Y + p[4].Y)) ||
                           (localPos.X >= p[0].X && localPos.X <= p[2].X && localPos.Y >= p[2].Y && localPos.Y <= (p[2].Y + p[4].Y));

                case RenderData.ShapeCategory.Blob:
                    return (localPos.X >= p[5].X && localPos.X <= p[1].X && localPos.Y >= p[0].Y && localPos.Y <= p[4].Y) &&
                           (localPos.X <= p[5].X || localPos.X >= p[0].X || localPos.Y <= p[0].Y || localPos.Y >= p[6].Y) &&
                           (localPos.X <= p[3].X || localPos.X >= p[1].X || localPos.Y <= p[2].Y || localPos.Y >= p[4].Y);
            }
            return false;
        }

        private bool IsOffsetInside(LayoutNode node, uint offset)
        {
            return (node.Offset <= offset && (node.Offset + node.Size) > offset);
        }

        private LayoutNode GetNodeAtPositionImpl(LayoutNode node, Point localPos, uint offset)
        {
            if (IsOffsetInside(node,offset) && IsPointInside(node.RenderData, localPos))
            {
                if (node.IsExpanded)
                {
                    if (node.ExpansionIndex.HasValue)
                    {
                        LayoutNode found = GetNodeAtPositionImpl(node.Children[node.ExpansionIndex.Value], localPos, offset);
                        if (found != null)
                        {
                            return found;
                        }
                    }
                    else
                    {
                        foreach (LayoutNode child in node.Children)
                        {
                            LayoutNode found = GetNodeAtPositionImpl(child, localPos, offset);
                            if (found != null)
                            {
                                return found;
                            }
                        }
                    }
                }

                return (GetSelectedDisplayMode() == DisplayMode.Stack || !node.IsExpanded)? node : null;
            }

            return null;
        }

        private LayoutNode GetNodeAtPosition(Point localPos)
        {
            if (Root == null || localPos.X < MarginLeft || localPos.Y < MarginTop)
            {
                return null;
            }

            uint column = (uint)((localPos.X- MarginLeft) / NodeWidth);
            uint row    = (uint)((localPos.Y- MarginTop) / NodeHeight);

            if (column >= DisplayGridColumns || row >= DisplayGridRows)
            {
                return null;
            }

            uint offset = GetOffset(row, column);

            return GetNodeAtPositionImpl(Root,localPos,offset);
        }

        private void ScrollViewer_OnLoaded(object sender, object e)
        {
            //Fix the issue with the colored corner square
            ((Rectangle)scrollViewer.Template.FindName("Corner", scrollViewer)).Fill = scrollViewer.Background;

            SetupCanvas();
            RenderGrid();
            RefreshShapes();
        }

        private void ScrollViewer_On2DMouseScroll(object sender, Mouse2DScrollEventArgs e)
        {
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta.X);
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta.Y);
        }

        private void SetHoverNodeFromPosition(Point p)
        {
            SetHoverNode(GetNodeAtPosition(p));
        }

        private void ScrollViewer_OnMouseMove(object sender, MouseEventArgs e)
        {
            Point p = e.GetPosition(canvas);
            SetHoverNodeFromPosition(p);
        }

        private void ScrollViewer_OnMouseLeave(object sender, MouseEventArgs e)
        {
            SetHoverNode(null);
        }

        private void TriggerNodeExpansion(LayoutNode node, int? index = null)
        {
            node.Expand(index);
            SetHoverNodeFromPosition(Mouse.GetPosition(canvas));
            RefreshShapes(); 
        }

        private void CreateChildSelectionMenu(LayoutNode node)
        {
            System.Windows.Forms.ContextMenuStrip contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
            for(int i = 0; i < node.Children.Count; ++i)
            {
                int index = i; //Copy to local variable in order to work for the click callback
                LayoutNode child = node.Children[index];
                var element = new System.Windows.Forms.ToolStripMenuItem();
                element.Text = String.IsNullOrEmpty(child.Name)? (String.IsNullOrEmpty(child.Type)? child.Category.ToString() : child.Type) : child.Name;
                element.Click += (sender, e) => TriggerNodeExpansion(node,index);
                contextMenuStrip.Items.Add(element);

            }
            contextMenuStrip.Show(System.Windows.Forms.Control.MousePosition);
        }

        private void OpenLocation(LayoutLocation location)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (location == null) return;
            EditorUtils.OpenFile(location.Filename, location.Line, location.Column);
        }

        private System.Windows.Forms.ToolStripMenuItem CreateContextMenu(string label, EventHandler onClick)
        {
            var element = new System.Windows.Forms.ToolStripMenuItem(label);
            element.Click += onClick;
            return element;
        }

        private void CreateContextualMenu(LayoutNode node)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            System.Windows.Forms.ContextMenuStrip contextMenuStrip = new System.Windows.Forms.ContextMenuStrip();

            bool hasFieldLocation = node.FieldLocation != null;
            bool hasTypeLocation = node.TypeLocation != null;
            bool hasParent = node.Parent != null;
            bool hasChildren = node.Children.Count > 0;

            bool hasName = node.Name.Length > 0; 
            bool hasType = node.Type.Length > 0;

            //Open Location
            if (hasFieldLocation)
            {
                contextMenuStrip.Items.Add(CreateContextMenu("Open Field Location", (sender, e) => OpenLocation(node.FieldLocation)));
            }

            if (hasTypeLocation)
            {
                contextMenuStrip.Items.Add(CreateContextMenu("Open Type Location", (sender, e) => OpenLocation(node.TypeLocation)));
            }

            //Separator
            if (contextMenuStrip.Items.Count > 0 && (hasParent || hasChildren))
            {
                contextMenuStrip.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            }

            //Expand/Collapse
            if (hasParent)
            {
                contextMenuStrip.Items.Add(CreateContextMenu("Collapse Parent", (sender, e) => ToggleCollapse(node.Parent)));
            }

            if (hasChildren)
            { 
                contextMenuStrip.Items.Add(CreateContextMenu(node.IsExpanded ? "Collapse" : "Expand", (sender, e) => ToggleCollapse(node)));
            }

            //Separator
            if (contextMenuStrip.Items.Count > 0 && (hasFieldLocation || hasTypeLocation || hasName || hasType))
            {
                contextMenuStrip.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            }

            //Clipboard
            if (hasName)
            {
                contextMenuStrip.Items.Add(CreateContextMenu("Copy Field Name", (sender, e) => Clipboard.SetText(node.Name)));
            }

            if (hasFieldLocation)
            {
                contextMenuStrip.Items.Add(CreateContextMenu("Copy Field Filename", (sender, e) => Clipboard.SetText(node.FieldLocation.Filename)));
            }

            if (hasType)
            {
                contextMenuStrip.Items.Add(CreateContextMenu("Copy Type Name", (sender, e) => Clipboard.SetText(node.Type)));
            }

            if (hasTypeLocation)
            {
                contextMenuStrip.Items.Add(CreateContextMenu("Copy Type Filename", (sender, e) => Clipboard.SetText(node.TypeLocation.Filename)));
            }

            contextMenuStrip.Show(System.Windows.Forms.Control.MousePosition);
        }

        private void ToggleCollapse(LayoutNode node)
        {
            if (node == null)
            {
                return;
            }

            if (node.IsExpanded)
            {
                CollapseNode(node);
                RefreshShapes();
            }
            else if (node.IsSharedMemory() && node.Children.Count > 1)
            {
                CreateChildSelectionMenu(node);
            }
            else if (node.Children.Count > 0)
            {
                TriggerNodeExpansion(node);
            }
        }

        private void ScrollViewer_OnClick(object sender, MouseButtonEventArgs e)
        {
            ToggleCollapse(Hover);
        }

        private void ScrollViewer_OnMouseContext(object sender, MouseButtonEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Hover != null)
            {
                CreateContextualMenu(Hover);
            }
        }

        private void DisplayAlign_Changed(object sender, object e)
        {
            if (displayAlignmentValue.Text.Length > 0)
            {
                uint value = Math.Max(1,Math.Min(Convert.ToUInt32(displayAlignmentValue.Text), 256));
                SetDisplayGridColumns(value);
            }
        }

        private void DisplayAlignmentComboBox_SelectionChanged(object sender, object e)
        {
            DisplayAlignmentType type = GetSelectedDisplayAlignment();
            switch(type)
            {
                case DisplayAlignmentType.Struct:    SetDisplayGridColumns(Root == null ? DisplayGridColumns : Root.Align);  break;
                case DisplayAlignmentType.Cacheline: SetDisplayGridColumns(CACHELINE_SIZE); break;
                case DisplayAlignmentType.Custom:    DisplayAlign_Changed(null, null); break;
            }
        }

        private void DisplayModeComboBox_SelectionChanged(object sender, object e)
        {
            RefreshNodeRenderData();

            if (Root != null && GetSelectedDisplayMode() == DisplayMode.Stack)
            {
                CollapseNode(Root);
                Root.Expand();
            }

            SetupCanvas();
            RenderGrid();
            RefreshShapes();
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void ButtonCollapseAll_Click(object sender, object e)
        {
            if (Root != null)
            {
                CollapseNode(Root);
                Root.Expand();
                RefreshShapes();
            }
        } 

        private void ButtonExpandAll_Click(object sender, object e)
        {
            if (Root != null)
            {
                ExpandAllNodes(Root);
                RefreshShapes();
            }
        } 
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
                OnControlMouseWheel.Invoke(this, e);
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
                On2DMouseScroll.Invoke(this, new Mouse2DScrollEventArgs(nextPosition - lastScrollingPosition));
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
        const uint CACHELINE_SIZE = 64;

        const double BaseNodeWidth  = 75;
        const double BaseNodeHeight = 25;

        const double MarginLeft   = 50;
        const double MarginRight  = 50;
        const double MarginTop    = 25;
        const double MarginBottom = 25;
        const double paddingSize  = 5; 

        private double NodeWidth  = BaseNodeWidth;
        private double NodeHeight = BaseNodeHeight;
        private double MaxDepth   = 0;
        private LayoutNode Root { set; get; }
        private LayoutNode Hover { set; get; }

        private ToolTip tooltip = new ToolTip { Content = new LayoutNodeTooltip() };

        private Pen nodeBorderPen = new Pen(Colors.GetCategoryForeground(), 2);

        private VisualHost baseVisual    = new VisualHost();
        private VisualHost gridVisual    = new VisualHost();
        private VisualHost overlayVisual = new VisualHost();

        private uint DisplayGridColumns = 4;
        private uint DisplayGridRows    = 0;

        public LayoutViewer()
        {
            InitializeComponent();
            this.DataContext = this;

            SetDisplayGridColumns(8);

            scrollViewer.Loaded += ScrollViewer_OnLoaded;
            scrollViewer.On2DMouseScroll += ScrollViewer_On2DMouseScroll;
            scrollViewer.MouseMove += ScrollViewer_OnMouseMove;
            scrollViewer.MouseLeave += ScrollViewer_OnMouseLeave;
            scrollViewer.MouseLeftButtonUp += ScrollViewer_OnClick;

            displayAlignmentValue.TextChanged += DisplayAlign_Changed;
        }

        public void SetLayout(LayoutNode root)
        {
            Root = root;
            SetHoverNode(null);

            uint displayAlignment = Root != null && daoStruct.IsChecked == true? root.Align : DisplayGridColumns;

            if (!SetDisplayGridColumns(displayAlignment))
            {
                RefreshNodeRenderData();
                SetupCanvas();
                RenderGrid();
                RefreshShapes();
            }
        }

        private void SetHoverNode(LayoutNode node)
        {
            if (node != Hover)
            {
                Hover = node;

                //Tooltip control 
                (tooltip.Content as LayoutNodeTooltip).ReferenceNode = Hover;
                tooltip.IsOpen = false;
                if (Hover != null)
                {
                    tooltip.Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse;
                    tooltip.IsOpen = true;
                    tooltip.PlacementTarget = this;
                }

                RenderOverlay();
            }
        }

        private void PrepareRenderData(LayoutNode node)
        {
            //Depth
            node.RenderData.Depth = node.Parent.RenderData.Depth + 1;
            MaxDepth = Math.Max(MaxDepth, node.RenderData.Depth);
           
            //Padding
            uint endOffsetParent = node.Parent.Offset + node.Parent.Size;
            uint parentLastOffset = endOffsetParent - 1;
            uint parentStartRow = GetRow(node.Parent.Offset);
            uint parentLastRow = GetRow(parentLastOffset);

            uint endOffset = node.Offset + node.Size;
            uint lastOffset = endOffset - 1;
            uint startCol = GetCol(node.Offset);
            uint startRow = GetRow(node.Offset);
            uint endCol = GetCol(endOffset);
            uint lastRow = GetRow(lastOffset);

            node.RenderData.PaddingFlags = RenderData.PaddingFlag.OuterLeft | RenderData.PaddingFlag.OuterRight;

            node.RenderData.PaddingFlags |= startCol == 0 || node.Offset == node.Parent.Offset? RenderData.PaddingFlag.InnerLeft  : 0;
            node.RenderData.PaddingFlags |= endCol == 0   || endOffset == endOffsetParent ? RenderData.PaddingFlag.InnerRight : 0;

            node.RenderData.PaddingFlags |= startRow <= parentStartRow+1 ? RenderData.PaddingFlag.OuterTop : 0;
            node.RenderData.PaddingFlags |= startRow == parentStartRow   ? RenderData.PaddingFlag.InnerTop : 0;

            node.RenderData.PaddingFlags |= lastRow+1 >= parentLastRow ? RenderData.PaddingFlag.OuterBottom : 0;
            node.RenderData.PaddingFlags |= lastRow == parentLastRow   ? RenderData.PaddingFlag.InnerBottom : 0;

            foreach (LayoutNode child in node.Children)
            {
                PrepareRenderData(child);
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

        private void ComputeRenderShape(LayoutNode node)
        {
            uint offset = node.Offset;
            uint size = node.Size;

            uint lastOffset = (offset + size) - 1;
            uint startRow = GetRow(offset);
            uint startCol = GetCol(offset);
            uint lastRow = GetRow(lastOffset);
            uint lastCol = GetCol(lastOffset);

            double paddingIR = GetPadding(node, RenderData.PaddingFlag.InnerRight);
            double paddingIL = GetPadding(node, RenderData.PaddingFlag.InnerLeft);
            double paddingOT = GetPadding(node, RenderData.PaddingFlag.OuterTop);
            double paddingOB = GetPadding(node, RenderData.PaddingFlag.OuterBottom);

            if (startRow == lastRow)
            {
                node.RenderData.Category = RenderData.ShapeCategory.Simple;
                node.RenderData.Points = new Point[2] {
                    new Point { X = MarginLeft + paddingIL + (startCol * NodeWidth), Y = MarginTop + paddingOT + (startRow * NodeHeight) }, //offset
                    new Point { X = size * NodeWidth - (paddingIL + paddingIR), Y = NodeHeight - (paddingOT + paddingOB) }, //size
                };
            }
            else
            {
                double paddingOL = GetPadding(node, RenderData.PaddingFlag.OuterLeft);
                double paddingOR = GetPadding(node, RenderData.PaddingFlag.OuterRight);
                double paddingIT = GetPadding(node, RenderData.PaddingFlag.InnerTop);
                double paddingIB = GetPadding(node, RenderData.PaddingFlag.InnerBottom);

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
        } 
            

        private void RefreshNodeRenderData()
        {
            if (Root != null)
            {
                MaxDepth = 0;
                Root.RenderData.Depth = 0;
                Root.RenderData.PaddingFlags = RenderData.PaddingFlag.All;

                foreach (LayoutNode child in Root.Children)
                {
                    PrepareRenderData(child);
                }

                //compute based on shape
                NodeWidth  = BaseNodeWidth + paddingSize * MaxDepth*2;
                NodeHeight = BaseNodeHeight + paddingSize * MaxDepth*2;

                ComputeRenderData(Root);
            } 
        }

        private bool SetDisplayGridColumns(uint input)
        {
            bool ret = false;

            if (DisplayGridColumns != input)
            {
                DisplayGridColumns = input;
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
            RenderOverlay();
        }

        private void RenderNode(DrawingContext drawingContext, LayoutNode node)
        {
            RenderBasicShape(drawingContext, node, node.RenderData.Background);

            //TODO ~ Ramonv ~ add label render

            if (!node.Collapsed)
            {
                foreach (LayoutNode child in node.Children)
                {
                    RenderNode(drawingContext, child);
                }
            }
        }

        private void RenderGridShape(DrawingContext drawingContext)
        {
            var pen = new Pen(Brushes.Black,1);

            uint numCols = DisplayGridColumns;
            uint numRows = DisplayGridRows;

            for (uint c = 0; c <= numCols;++c)
            {
                double h = MarginLeft + (c * NodeWidth);
                drawingContext.DrawLine(pen, new Point(h,0), new Point(h,canvas.Height-MarginBottom));
            }

            for (uint r = 0; r <= numRows;++r)
            {
                double h = MarginTop+(r*NodeHeight);
                drawingContext.DrawLine(pen, new Point(0, h), new Point(canvas.Width-MarginRight, h));
            }

            //Draw Labels



            /*
             * 
             *    //Render text
            if (screenWidth >= textRenderMinWidth)
            {
                var UIText = new FormattedText(node.Label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, 12, Common.Colors.GetCategoryForeground(), VisualTreeHelper.GetDpi(this).PixelsPerDip);
                UIText.MaxTextWidth = Math.Min(screenWidth, UIText.Width);
                UIText.MaxTextHeight = NodeHeight;

                double textPosX = (pixelEnd + pixelStart - UIText.Width) * 0.5;
                double textPosY = posY + (NodeHeight - UIText.Height) * 0.5;

                drawingContext.DrawText(UIText, new Point(textPosX, textPosY));

            }*/

        }

        private double GetPadding(LayoutNode node, RenderData.PaddingFlag flag)
        {
            return (node.RenderData.PaddingFlags.HasFlag(flag)? node.RenderData.Depth : 1) * paddingSize;
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

        private void RenderBase()
        {
            using (DrawingContext drawingContext = baseVisual.Visual.RenderOpen())
            {
                if (Root != null)
                {
                    RenderNode(drawingContext, Root);
                }
            }

            //force a canvas redraw
            RefreshCanvasVisual(baseVisual);
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
                    //TODO ~ ramonv ~ render the collapse opposite
                    var overlayBrush = Brushes.White.Clone(); ///TODO ~ ramonv move outside
                    overlayBrush.Opacity = 0.3;

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

        /*
        private void ExpandAllNodes()
        {
            //TODO ~ ramonv ~ to be implemented
        }
        */
            private bool ExpandNode(LayoutNode node)
        {
            bool ret = node.Collapsed;
            node.Collapsed = false;
            return ret;
        }

        private bool CollapseNode(LayoutNode node)
        {
            if (!node.Collapsed)
            {
                node.Collapsed = true;
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

        private LayoutNode GetNodeAtPositionImpl(LayoutNode node, Point localPos, uint offset)
        {
            if (node.Offset > offset || (node.Offset + node.Size) <= offset || !IsPointInside(node.RenderData, localPos))
            {
                return null;
            }

            if (!node.Collapsed)
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

            return node;
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
            SetupCanvas();
            RenderGrid();
            RefreshShapes();
        }

        private void ScrollViewer_On2DMouseScroll(object sender, Mouse2DScrollEventArgs e)
        {
            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta.X);
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta.Y);
        }

        private void SetHoverNodeFromMouseEvent(MouseEventArgs e)
        {
            Point p = e.GetPosition(canvas);
            SetHoverNode(GetNodeAtPosition(e.GetPosition(canvas)));
        }

        private void ScrollViewer_OnMouseMove(object sender, MouseEventArgs e)
        {
            SetHoverNodeFromMouseEvent(e);
        }

        private void ScrollViewer_OnMouseLeave(object sender, MouseEventArgs e)
        {
            SetHoverNode(null);
        }

        private void ScrollViewer_OnClick(object sender, MouseButtonEventArgs e)
        {
            if (Hover != null && Hover.Children.Count > 0)
            {
                if (Hover.Collapsed)
                {
                    ExpandNode(Hover);
                    SetHoverNodeFromMouseEvent(e);
                }
                else
                {
                    CollapseNode(Hover);
                }

                RefreshShapes();
            }
        }

        private void DisplayAlign_Changed(object sender, object e)
        {
            if (displayAlignmentValue.Text.Length > 0)
            {
                uint value = Math.Min(Convert.ToUInt32(displayAlignmentValue.Text), 200);
                SetDisplayGridColumns(value);
            }
        }

        private void dao_Checked(object sender, RoutedEventArgs e)
        {
            if (daoStruct.IsChecked == true)
            {
                SetDisplayGridColumns(Root == null ? DisplayGridColumns : Root.Align );
            } 
            else if (daoCache.IsChecked == true)
            {
                SetDisplayGridColumns(CACHELINE_SIZE);
            }
            else if (daoCustom.IsChecked == true)
            {
                //Set the value inside the custom box
                DisplayAlign_Changed(null, null);
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}

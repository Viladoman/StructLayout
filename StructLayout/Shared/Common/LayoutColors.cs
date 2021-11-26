namespace StructLayout
{
    using System.Windows.Media;

    class Colors
    {
        static Brush RootBrush     = new SolidColorBrush(Color.FromRgb(75, 75, 75));
        static Brush SimpleBrush   = new SolidColorBrush(Color.FromRgb(170, 115, 0));
        static Brush BitfieldBrush = new SolidColorBrush(Color.FromRgb(0, 85, 85));
        static Brush ComplexBrush  = new SolidColorBrush(Color.FromRgb(85, 0, 85));
        static Brush BaseBrush     = new SolidColorBrush(Color.FromRgb(51, 119, 102));
        static Brush VTableBrush   = new SolidColorBrush(Color.FromRgb(0, 119, 51));
        static Brush UnionBrush    = new SolidColorBrush(Color.FromRgb(0, 0, 119));
        static Brush SharedBrush   = new SolidColorBrush(Color.FromRgb(20, 20, 119));
        static Brush OtherBrush    = new SolidColorBrush(Color.FromRgb(51, 51, 51));

        static public Brush GetCategoryBackground(LayoutNode.LayoutCategory category)
        {
            switch (category)
            {
                case LayoutNode.LayoutCategory.Root:           return RootBrush;
                case LayoutNode.LayoutCategory.SimpleField:    return SimpleBrush;
                case LayoutNode.LayoutCategory.Bitfield:       return BitfieldBrush;
                case LayoutNode.LayoutCategory.ComplexField:   return ComplexBrush;
                case LayoutNode.LayoutCategory.VPrimaryBase:   return BaseBrush;
                case LayoutNode.LayoutCategory.VBase:          return BaseBrush;
                case LayoutNode.LayoutCategory.NVPrimaryBase:  return BaseBrush;
                case LayoutNode.LayoutCategory.NVBase:         return BaseBrush;
                case LayoutNode.LayoutCategory.VTablePtr:      return VTableBrush;
                case LayoutNode.LayoutCategory.VFTablePtr:     return VTableBrush;
                case LayoutNode.LayoutCategory.VBTablePtr:     return VTableBrush;
                case LayoutNode.LayoutCategory.VtorDisp:       return VTableBrush;
                case LayoutNode.LayoutCategory.Union:          return UnionBrush;
                case LayoutNode.LayoutCategory.Shared:         return SharedBrush;
                default: return OtherBrush;
            }
        }

        static public Brush GetCategoryForeground()
        {
            return Brushes.White;
        }
    }
}

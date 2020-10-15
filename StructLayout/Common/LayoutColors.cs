namespace StructLayout
{
    using System.Windows.Media;

    class Colors
    {
        static Brush IncludeBrush = new SolidColorBrush(Color.FromRgb(85, 0, 85));
        static Brush ParseClassBrush = new SolidColorBrush(Color.FromRgb(170, 115, 0));
        static Brush ParseTemplateBrush = new SolidColorBrush(Color.FromRgb(170, 115, 51));
        static Brush InstantiateClassBrush = new SolidColorBrush(Color.FromRgb(0, 119, 0));
        static Brush InstantiateFuncBrush = new SolidColorBrush(Color.FromRgb(0, 119, 51));
        static Brush InstantiateVariableBrush = new SolidColorBrush(Color.FromRgb(51, 119, 0));
        static Brush InstantiateConceptBrush = new SolidColorBrush(Color.FromRgb(51, 119, 51));
        static Brush CodeGenBrush = new SolidColorBrush(Color.FromRgb(3, 71, 54));
        static Brush PendingInstantiationBrush = new SolidColorBrush(Color.FromRgb(0, 0, 119));
        static Brush OptModuleBrush = new SolidColorBrush(Color.FromRgb(119, 51, 17));
        static Brush OptFunctionBrush = new SolidColorBrush(Color.FromRgb(45, 66, 98));
        static Brush RunPassBrush = new SolidColorBrush(Color.FromRgb(0, 85, 85));
        static Brush FrontEndBrush = new SolidColorBrush(Color.FromRgb(136, 136, 0));
        static Brush BackEndBrush = new SolidColorBrush(Color.FromRgb(136, 81, 0));
        static Brush ExecuteCompilerBrush = new SolidColorBrush(Color.FromRgb(51, 119, 102));
        static Brush ThreadBrush = new SolidColorBrush(Color.FromRgb(75, 75, 75));
        static Brush TimelineBrush = new SolidColorBrush(Color.FromRgb(51, 51, 51));


        static Brush RootBrush     = new SolidColorBrush(Color.FromRgb(75, 75, 75));
        static Brush SimpleBrush   = new SolidColorBrush(Color.FromRgb(170, 115, 0));
        static Brush BitfieldBrush = new SolidColorBrush(Color.FromRgb(0, 85, 85));
        static Brush ComplexBrush  = new SolidColorBrush(Color.FromRgb(85, 0, 85));
        static Brush BaseBrush     = new SolidColorBrush(Color.FromRgb(51, 119, 102));
        static Brush VTableBrush   = new SolidColorBrush(Color.FromRgb(0, 119, 51));
        static Brush PaddingBrush  = new SolidColorBrush(Color.FromRgb(119, 0, 0));
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
                case LayoutNode.LayoutCategory.Padding:        return PaddingBrush;
                default: return OtherBrush;
            }
        }

        static public Brush GetCategoryForeground()
        {
            return Brushes.White;
        }
    }
}

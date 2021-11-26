using Microsoft.VisualStudio.Shell;

namespace StructLayout.Common
{
    static public class ColorTheme
    {
        public static readonly object Background                  = VsBrushes.WindowKey;
        public static readonly object Foreground                  = VsBrushes.WindowTextKey;

        public static readonly object ComboBox_MouseOverBackground = VsBrushes.ToolWindowTabMouseOverBackgroundBeginKey;
        public static readonly object ComboBox_MouseOverForeground = VsBrushes.ToolWindowTabMouseOverTextKey;
    }
}

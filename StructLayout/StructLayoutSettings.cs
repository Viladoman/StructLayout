using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace StructLayout
{
    public class GeneralSettingsPageGrid : DialogPage
    {
        [Category("Parser")]
        [DisplayName("Print Command Line")]
        [Description("Print the command line argument passed to the parser in the Tool output pane")]
        public bool OptionParserShowCommandLine { set; get; } = false;

        /*
        [Category("Viewer")]
        [DisplayName("Grid Number Base")]
        [Description("Base for the numbers in the viewer grid rows and columns")]
        public LayoutViewer.GridBase OptionViewerGridBase { set; get; } = LayoutViewer.GridBase.Hexadecimal;
        */
    }
}



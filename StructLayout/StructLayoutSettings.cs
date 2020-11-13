using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace StructLayout
{
    public class GeneralSettingsPageGrid : DialogPage
    {
        private LayoutViewer.GridBase gridNumberBase = LayoutViewer.GridBase.Hexadecimal;

        [Category("Parser")]
        [DisplayName("Print Command Line")]
        [Description("Print the command line argument passed to the parser in the Tool output pane")]
        public bool OptionParserShowCommandLine { set; get; } = true;

        [Category("Viewer")]
        [DisplayName("Grid Number Base")]
        [Description("Base for the numbers in the viewer grid rows and columns")]
        public LayoutViewer.GridBase OptionViewerGridBase { 
            get { return gridNumberBase; } 
            set { gridNumberBase = value; ThreadHelper.ThrowIfNotOnUIThread(); EditorProcessor.Instance.OnUserSettingsChanged(); } 
        }
    }
}



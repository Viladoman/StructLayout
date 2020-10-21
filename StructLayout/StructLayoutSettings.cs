using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace StructLayout
{
    public class GeneralSettingsPageGrid : DialogPage
    {
        //TODO ~ ramonv ~ add VS style Additional Include Paths
        //TODO ~ ramonv ~ add VS style Additional Preprocessor definitions
        //TODO ~ ramonv ~ add VS style Additional Forced includes

        [Category("Parser")]
        [DisplayName("Print Command Line")]
        [Description("Print the command line argument passed to the parser in the Tool output pane")]
        public bool OptionParserShowCommandLine { set; get; } = false;

        [Category("Parser")]
        [DisplayName("Additional arguments")]
        [Description("Add extra arguments for the parser")]
        public string OptionParserExtraArguments { set; get; } = "";

        [Category("Parser")]
        [DisplayName("Show warnings")]
        [Description("If true it will display the C++ warnings found while parsing the file")]
        public bool OptionParserShowWarnings { set; get; } = false;

        [Category("Viewer")]
        [DisplayName("Grid Number Base")]
        [Description("Base for the numbers in the viewer grid rows and columns")]
        public LayoutViewer.GridBase OptionViewerGridBase { set; get; } = LayoutViewer.GridBase.Hexadecimal;
    }
}



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

        [Category("Parser")]
        [DisplayName("Additional arguments")]
        [Description("Add extra arguments for the parser")]
        public string OptionParserExtraArguments { set; get; } = "";

        /*
        public enum GridBase
        {
            Decimal,
            Hexadecimal,
        }

        [Category("Viewer")]
        [DisplayName("Grid Number Base")]
        [Description("Base for the numbers in the viewer grid rows and columns")]
        public GridBase OptionViewerGridBase { set; get; } = GridBase.Hexadecimal;
        */
    }
}



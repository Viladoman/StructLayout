using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace StructLayout
{
    public class GeneralSettingsPageGrid : DialogPage
    {
        [Category("Parser")]
        [DisplayName("Print Command Line")]
        [Description("Print the command line argument passed to the parser in the Tool output pane")]
        public bool OptionParserShowCommandLine { set; get; } = true;

        [Category("Viewer")]
        [DisplayName("Grid Number Base")]
        [Description("Base for the numbers in the viewer grid rows and columns")]
        public LayoutViewer.GridBase OptionViewerGridBase { 
            get { return LayoutViewer.DefaultGridNumberBase; } 
            set { LayoutViewer.DefaultGridNumberBase = value; ThreadHelper.ThrowIfNotOnUIThread(); EditorProcessor.Instance.OnUserSettingsChanged(); } 
        }

        [Category("Viewer")]
        [DisplayName("Default Display Alignment")]
        [Description("Default value for the viewer display alignment")]
        public LayoutViewer.DisplayAlignmentType OptionDefaultDisplayAlignment 
        { 
            get { return LayoutViewer.DefaultDisplayAlignment; } 
            set { LayoutViewer.DefaultDisplayAlignment = value; }
        }
        
        [Category("Viewer")]
        [DisplayName("Default Display Alignment Custom")]
        [Description("The custom alignment to set by default")]
        public uint OptionDefaultDisplayAlignmentCustomValue
        {
            get { return LayoutViewer.DefaultDisplayCustomAlignment; }
            set { LayoutViewer.DefaultDisplayCustomAlignment = value; }
        }
        
        [Category("Viewer")]
        [DisplayName("Default Display Mode")]
        [Description("Default value for the viewer display mode")]
        public LayoutViewer.DisplayMode OptionDefaultDisplayMode
        {
            get { return LayoutViewer.DefaultDisplayMode; }
            set { LayoutViewer.DefaultDisplayMode = value; }
        }

    }
}



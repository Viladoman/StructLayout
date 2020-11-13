namespace StructLayout
{
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for LayoutWindowControl.
    /// </summary>
    public partial class LayoutWindowControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LayoutWindowControl"/> class.
        /// </summary>
        public LayoutWindowControl()
        {
            this.InitializeComponent();

            SetDefaultStatus();
        }
        public void SetDefaultStatus()
        {
            statusText.Text = "Inspecting: <none>";
        }

        public void SetProcessing()
        {
            statusText.Text = "Processing...";
        }

        public void SetResult(ParseResult result)
        {
            statusText.Text = "Inspecting: "+ (result.Layout == null ? "<none>" : result.Layout.Type) + " - ";

            //add details
            switch (result.Status)
            {
                case ParseResult.StatusCode.InvalidInput: statusText.Text += "Invalid Input"; break;
                case ParseResult.StatusCode.ParseFailed:  statusText.Text += "Parse Error"; break;
                case ParseResult.StatusCode.NotFound:     statusText.Text += "Nothing found at the given position"; break;
                case ParseResult.StatusCode.Found:        statusText.Text += "Size: "+ LayoutNodeTooltip.GetFullValueStr(result.Layout.Size)+" - Align: "+ LayoutNodeTooltip.GetFullValueStr(result.Layout.Align); break;
                default: SetDefaultStatus(); break;
            }

            viewer.SetLayout(result.Layout);
        }

        public void SetGridNumberBase(LayoutViewer.GridBase gridBase)
        {
            viewer.SetGridNumberBase(gridBase);
        }

        public void OpenSettings(object a, object e)
        {
            SettingsManager.Instance.OpenSettingsWindow();
        }
    }
}
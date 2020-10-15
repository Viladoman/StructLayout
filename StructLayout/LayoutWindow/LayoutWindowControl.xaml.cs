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
        }

        public void SetLayout(LayoutNode node)
        {
            viewer.SetLayout(node);
        }
    }
}
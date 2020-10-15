using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace StructLayout
{
    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("c1b1ecb5-9ef7-4b66-8864-e11f8917bc78")]
    public class LayoutWindow : Common.WindowProxy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LayoutWindow"/> class.
        /// </summary>
        public LayoutWindow()
        {
            this.Caption = "Struct Layout";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new LayoutWindowControl();
        }

        public void SetLayout(LayoutNode node)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            (this.Content as LayoutWindowControl).SetLayout(node);
        }
    }
}

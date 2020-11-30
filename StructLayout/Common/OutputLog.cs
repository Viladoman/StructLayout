using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;

namespace StructLayout
{
    public static class OutputLog
    {
        private static IServiceProvider ServiceProvider { set; get; }

        private static IVsOutputWindowPane paneInstance;

        private static IVsOutputWindowPane Pane 
        { 
            get 
            {
                //Lazy creation to reduce noise. It will be created on the first request
                ThreadHelper.ThrowIfNotOnUIThread();
                if (paneInstance == null)
                {
                    paneInstance = CreatePane(ServiceProvider, Guid.NewGuid(), "Struct Layout", true, false);
                }
                return paneInstance;
            } 
        }

        public static void Initialize(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public static void Clear()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Pane.Clear();
        }

        public static void Focus()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Pane.Activate();
        }

        public static void Log(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Write(text);
        }

        public static void Error(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Write("[ERROR] " + text);
        }

        private static void Write(string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            DateTime currentTime = DateTime.Now;
            Pane.OutputString("[" + String.Format("{0:HH:mm:ss}", currentTime) + "] " + text + "\n");            
        }

        private static IVsOutputWindowPane CreatePane(IServiceProvider serviceProvider, Guid paneGuid, string title, bool visible, bool clearWithSolution)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IVsOutputWindow output = (IVsOutputWindow)serviceProvider.GetService(typeof(SVsOutputWindow));
            Assumes.Present(output);

            // Create a new pane.
            output.CreatePane(ref paneGuid, title, Convert.ToInt32(visible), Convert.ToInt32(clearWithSolution));

            // Retrieve the new pane.
            IVsOutputWindowPane pane;
            output.GetPane(ref paneGuid, out pane);
            return pane;
        }
    }
}

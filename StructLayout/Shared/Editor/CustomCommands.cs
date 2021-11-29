using System;
using System.ComponentModel.Design;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace StructLayout
{
    class CustomCommands
    {
        public const int CommandId_Layout = 0x0100;

        public static readonly Guid CommandSet_Windows = new Guid("7df6381a-21a4-4a2b-b334-faac37766f18");

        public const int CommandId_Parse = 256;

        public static readonly Guid CommandSet_Code = new Guid("1dffa616-1620-4243-92e7-6e5efdc8e05d");

        public const int CommandId_Settings = 256;
        public const int CommandId_Documentation = 257;
        public const int CommandId_ReportIssue = 258;
        public const int CommandId_About = 259;

        public static readonly Guid CommandSet_Custom = new Guid("2d97936f-81d9-4101-a448-a39c1e21596a");

        private static IServiceProvider ServiceProvider { set; get; }

        public static async Task InitializeAsync(AsyncPackage package, IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;

            // Switch to the main thread - the call to AddCommand in Build's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Assumes.Present(commandService);

            commandService.AddCommand(new MenuCommand(Execute_LayoutWindow, new CommandID(CommandSet_Windows, CommandId_Layout)));

            commandService.AddCommand(new MenuCommand(Execute_Parse, new CommandID(CommandSet_Code, CommandId_Parse)));

            commandService.AddCommand(new MenuCommand(Execute_Settings, new CommandID(CommandSet_Custom, CommandId_Settings)));
            commandService.AddCommand(new MenuCommand(Execute_Documentation, new CommandID(CommandSet_Custom, CommandId_Documentation)));
            commandService.AddCommand(new MenuCommand(Execute_ReportIssue, new CommandID(CommandSet_Custom, CommandId_ReportIssue)));
            commandService.AddCommand(new MenuCommand(Execute_About, new CommandID(CommandSet_Custom, CommandId_About)));
        }

        private static void Execute_LayoutWindow(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            LayoutWindow win = EditorUtils.GetLayoutWindow(true);
            if (win != null)
            {
                EditorUtils.FocusWindow(win);
            }
        }

        private static void Execute_Parse(object sender, EventArgs e)
        {
            //fire and forget
            _ = EditorProcessor.Instance.ParseAtCurrentLocationAsync();
        }

        private static void Execute_About(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            AboutWindow dlg = new AboutWindow();
            dlg.ShowDialog();
        }

        private static void Execute_Documentation(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Documentation.OpenLink(Documentation.Link.MainPage);
        }

        private static void Execute_ReportIssue(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Documentation.OpenLink(Documentation.Link.ReportIssue);
        }

        private static void Execute_Settings(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            SettingsManager.Instance.OpenSettingsWindow();
        }
    }
}

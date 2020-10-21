using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace StructLayout
{
    public class EditorProcessor
    {
        private static readonly Lazy<EditorProcessor> lazy = new Lazy<EditorProcessor>(() => new EditorProcessor());
        public static EditorProcessor Instance { get { return lazy.Value; } }

        LayoutParser parser = new LayoutParser();

        private StructLayoutPackage Package { get; set; }
        private IServiceProvider ServiceProvider { get; set; }

        public void Initialize(StructLayoutPackage package)
        {
            Package = package;
            ServiceProvider = package;
        }

        public GeneralSettingsPageGrid GetGeneralSettings()
        {
            return (GeneralSettingsPageGrid)Package.GetDialogPage(typeof(GeneralSettingsPageGrid));
        }

        private void SaveActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //Get full file path
            var applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            Assumes.Present(applicationObject);

            Document doc = applicationObject.ActiveDocument;
            if (doc != null && !doc.ReadOnly)
            {
                doc.Save();
            }
        }

        private DocumentLocation GetCurrentLocation()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //Get full file path
            var applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            Assumes.Present(applicationObject);

            string filename = applicationObject.ActiveDocument.FullName;

            //Get text location
            var textManager = ServiceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            if (textManager == null) return null;

            IVsTextView view;
            textManager.GetActiveView2(1, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out view);
            if (view == null) return null;

            view.GetCaretPos(out int line, out int col);

            return new DocumentLocation(filename,(uint)(line+1),(uint)(col+1));
        }

        private Project GetActiveProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            Assumes.Present(applicationObject);
            if (applicationObject.ActiveDocument == null || applicationObject.ActiveDocument.ProjectItem == null) return null;
            return applicationObject.ActiveDocument.ProjectItem.ContainingProject;
        }

        private Solution GetActiveSolution()
        {
            DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
            Assumes.Present(applicationObject);
            return applicationObject.Solution;
        }

        private bool IsMSBuildStringInvalid(string input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (input.Length == 0)
            {
                return true;
            }

            if (input.Contains('$'))
            {
                OutputLog.Log("Dropped " + input + ". It contains an unknown MSBuild macro");
                return true;
            }
            return false;
        }

        private void AppendMSBuildStringToList(List<string> list, string input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var split = input.Split(';').ToList(); //Split
            split.RemoveAll(s => IsMSBuildStringInvalid(s)); //Validate

            foreach(string str in split)
            {
                if (!list.Contains(str))
                {
                    list.Add(str);
                }
            }
        }

        private string GetPathDirectory(string input)
        {
            return (Path.HasExtension(input) ? Path.GetDirectoryName(input) : input) + '\\';           
        }

        private void AppendProjectProperties(ProjectProperties properties, VCCLCompilerTool cl, VCNMakeTool nmake, VCPlatform platform)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (cl != null)
            {
                AppendMSBuildStringToList(properties.IncludeDirectories,     platform.Evaluate(cl.AdditionalIncludeDirectories));
                AppendMSBuildStringToList(properties.ForceIncludes,          platform.Evaluate(cl.ForcedIncludeFiles));
                AppendMSBuildStringToList(properties.PrepocessorDefinitions, platform.Evaluate(cl.PreprocessorDefinitions));
            }
            else if (nmake != null)
            {
                AppendMSBuildStringToList(properties.IncludeDirectories,     platform.Evaluate(nmake.IncludeSearchPath));
                AppendMSBuildStringToList(properties.ForceIncludes,          platform.Evaluate(nmake.ForcedIncludes));
                AppendMSBuildStringToList(properties.PrepocessorDefinitions, platform.Evaluate(nmake.PreprocessorDefinitions));
            }
        }

        private ProjectProperties GetProjectData()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Solution solution = GetActiveSolution();
            if (solution == null) return null;

            Project project = GetActiveProject();
            if (project == null) return null; 

            VCProject prj = project.Object as VCProject;
            if (prj == null) return null;

            VCConfiguration config = prj.ActiveConfiguration;
            if (config == null) return null;

            VCPlatform platform = config.Platform;
            if (platform == null) return null;

            var vctools = config.Tools as IVCCollection;
            var midl = vctools.Item("VCMidlTool") as VCMidlTool;

            ProjectProperties ret = new ProjectProperties();
            ret.Target = midl != null && midl.TargetEnvironment == midlTargetEnvironment.midlTargetWin32 ? ProjectProperties.TargetType.x86 : ProjectProperties.TargetType.x64;

            //Include dirs / files and preprocessor
            AppendMSBuildStringToList(ret.IncludeDirectories, platform.Evaluate(platform.IncludeDirectories));
            AppendProjectProperties(ret, vctools.Item("VCCLCompilerTool") as VCCLCompilerTool, vctools.Item("VCNMakeTool") as VCNMakeTool, platform);
       
            //Get settings from the single file 

            //TODO ~ ramonv ~ think of a way to handle .cpp files too when .h are parsed
            var applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            Assumes.Present(applicationObject);
            ProjectItem item = applicationObject.ActiveDocument.ProjectItem;
            VCFile vcfile = item.Object as VCFile;

            IVCCollection fileCfgs = (IVCCollection)vcfile.FileConfigurations;
            VCFileConfiguration fileConfig = fileCfgs.Item(config.Name) as VCFileConfiguration;

            AppendProjectProperties(ret, fileConfig.Tool as VCCLCompilerTool, fileConfig.Tool as VCNMakeTool, platform);

            return ret;
        }

        public LayoutWindow GetLayoutWindow(bool create = true)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            LayoutWindow window = Package.FindToolWindow(typeof(LayoutWindow), 0, create) as LayoutWindow;
            if ((null == window) || (null == window.GetFrame()))
            {
                return null;
            }

            return window;
        }

        public void FocusWindow(LayoutWindow window)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (window != null)
            {
                window.ProxyShow();
            }
        }

        public async System.Threading.Tasks.Task ParseAtCurrentLocationAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            //TODO ~ ramonv ~ add parsing queue

            OutputLog.Clear();

            SaveActiveDocument();

            DocumentLocation location = GetCurrentLocation();
            if (location == null)
            {
                OutputLog.Error("Unable to retrieve current document position.");
                return;
            }

            ProjectProperties properties = GetProjectData();
            if (properties == null)
            {
                OutputLog.Error("Unable to retrieve the project configuration");
                return;
            }

            GeneralSettingsPageGrid settings = GetGeneralSettings();
            parser.ExtraArgs = settings.OptionParserExtraArguments;
            parser.PrintCommandLine = settings.OptionParserShowCommandLine;
            parser.ShowWarnings = settings.OptionParserShowWarnings;

            //TODO ~ ramonv ~ notify the window about the parsing start
            /*
            var prewin = GetLayoutWindow(false);
            prewin.SetProcessing();
            */

            var layout = await parser.ParseAsync(properties, location);

            var win = GetLayoutWindow(true);
            win.SetLayout(layout);
            if (layout != null)
            {
                FocusWindow(win);
            }
        }
    }
}

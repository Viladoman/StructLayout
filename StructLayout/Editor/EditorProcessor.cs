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

            applicationObject.ActiveDocument.Save();
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

        private  string MultipleReplace(string text, Dictionary<string,string> replacements)
        {
            string ret = text;
            foreach (KeyValuePair<string, string> entry in replacements)
            {
                ret = ret.Replace(entry.Key,entry.Value);
            }
            return ret; 

            //return Regex.Replace(text, "(" + String.Join("|", replacements.Keys.ToArray()) + ")",  delegate (Match m) { return replacements[m.Value]; } );
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

        private List<string> ProcessMSBuildStringToList(string input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var list = input.Split(';').ToList(); //Split
            list.RemoveAll(s => IsMSBuildStringInvalid(s)); //Validate
            return list;
        }

        private List<string> ProcessMSBuildPaths(string input, Dictionary<string, string> MSBuildMacros)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string replaced = MultipleReplace(input, MSBuildMacros);
            return ProcessMSBuildStringToList(replaced);
        }

        private string GetPathDirectory(string input)
        {
            return (Path.HasExtension(input) ? Path.GetDirectoryName(input) : input) + '\\';           
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

            var vctools = config.Tools as IVCCollection;
            var midl = vctools.Item("VCMidlTool") as VCMidlTool;
            var cl = vctools.Item("VCCLCompilerTool") as VCCLCompilerTool;
            var nmake = vctools.Item("VCNMakeTool") as VCNMakeTool;

            if (cl == null && nmake == null)
            {
                //TODO ~ ramonv ~ fallback to something different and custom
                // Not supported atm 
                return null;
            }

            ProjectProperties ret = new ProjectProperties();
            ret.Target = midl != null && midl.TargetEnvironment == midlTargetEnvironment.midlTargetWin32 ? ProjectProperties.TargetType.x86 : ProjectProperties.TargetType.x64;

            //build MSBuildMacros for replacement
            var MSBuildMacros = new Dictionary<string, string>();

            MSBuildMacros.Add(@"$(Configuration)",    config.Name);
            MSBuildMacros.Add(@"$(IntDir)",           config.IntermediateDirectory);
            MSBuildMacros.Add(@"$(OutDir)",           config.OutputDirectory);
            MSBuildMacros.Add(@"$(TargetDir)",        config.OutputDirectory);

            MSBuildMacros.Add(@"$(SolutionPath)",     solution.FullName);
            MSBuildMacros.Add(@"$(SolutionDir)",      GetPathDirectory(solution.FullName));
            MSBuildMacros.Add(@"$(SolutionExt)",      Path.GetExtension(solution.FullName));
            MSBuildMacros.Add(@"$(SolutionFileName)", Path.GetFileName(solution.FullName));
            //MSBuildMacros.Add(@"$(SolutionName)", );

            MSBuildMacros.Add(@"$(ProjectPath)",      project.FullName);
            MSBuildMacros.Add(@"$(ProjectDir)",       GetPathDirectory(project.FullName));
            MSBuildMacros.Add(@"$(ProjectExt)",       Path.GetExtension(project.FullName));
            MSBuildMacros.Add(@"$(ProjectFileName)",  Path.GetFileName(project.FullName));
            MSBuildMacros.Add(@"$(ProjectName)",      project.Name);

            if (cl != null)
            {
                ret.IncludeDirectories = ProcessMSBuildPaths(cl.AdditionalIncludeDirectories, MSBuildMacros);
                ret.PrepocessorDefinitions = ProcessMSBuildStringToList(cl.PreprocessorDefinitions);
            }
            else
            {
                ret.IncludeDirectories = ProcessMSBuildPaths(nmake.IncludeSearchPath, MSBuildMacros);
                ret.PrepocessorDefinitions = ProcessMSBuildStringToList(nmake.PreprocessorDefinitions); 
            }

            return ret;
        }

        public LayoutWindow GetLayoutWindow()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            LayoutWindow window = Package.FindToolWindow(typeof(LayoutWindow), 0, true) as LayoutWindow;
            if ((null == window) || (null == window.GetFrame()))
            {
                throw new NotSupportedException("Cannot create tool window");
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

        public void ParseAtCurrentLocation()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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

            LayoutNode layout = parser.Parse(properties, location);

            var win = GetLayoutWindow();
            win.SetLayout(layout);

            //TODO ~ ramonv ~ only when no errors happened
            FocusWindow(win);
        }
    }
}

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

        private DocumentLocation GetCurrentLocation()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Document activeDocument = EditorUtils.GetActiveDocument();
            if (activeDocument == null) return null;

            //Get text location
            var textManager = EditorUtils.ServiceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            if (textManager == null) return null;

            IVsTextView view;
            textManager.GetActiveView2(1, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out view);
            if (view == null) return null;

            view.GetCaretPos(out int line, out int col);

            return new DocumentLocation(activeDocument.FullName, (uint)(line+1),(uint)(col+1));
        }

        private ProjectProperties.StandardVersion GetStandardVersion(VCConfiguration config)
        {
            IVCRulePropertyStorage generalRule = config.Rules.Item("ConfigurationGeneral");
            string value = null;

            try { value = generalRule == null? null : generalRule.GetEvaluatedPropertyValue("LanguageStandard"); }catch(Exception){}
            
                 if (value == "Default")      { return ProjectProperties.StandardVersion.Default; }
            else if (value == "stdcpp14")     { return ProjectProperties.StandardVersion.Cpp14; }
            else if (value == "stdcpp17")     { return ProjectProperties.StandardVersion.Cpp17; }
            else if (value == "stdcpplatest") { return ProjectProperties.StandardVersion.Latest; }

            return ProjectProperties.StandardVersion.Latest;
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

        private void RemoveMSBuildStringFromList(List<string> list, string input)
        {
            var split = input.Split(';').ToList(); //Split

            foreach (string str in split)
            {
                list.Remove(str);
            }
        }

        private void AppendProjectProperties(ProjectProperties properties, VCCLCompilerTool cl, VCNMakeTool nmake, IMacroEvaluator evaluator)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (cl != null)
            {
                AppendMSBuildStringToList(properties.IncludeDirectories,     evaluator.Evaluate(cl.AdditionalIncludeDirectories));
                AppendMSBuildStringToList(properties.ForceIncludes,          evaluator.Evaluate(cl.ForcedIncludeFiles));
                AppendMSBuildStringToList(properties.PrepocessorDefinitions, evaluator.Evaluate(cl.PreprocessorDefinitions));
            }
            else if (nmake != null)
            {
                AppendMSBuildStringToList(properties.IncludeDirectories,     evaluator.Evaluate(nmake.IncludeSearchPath));
                AppendMSBuildStringToList(properties.ForceIncludes,          evaluator.Evaluate(nmake.ForcedIncludes));
                AppendMSBuildStringToList(properties.PrepocessorDefinitions, evaluator.Evaluate(nmake.PreprocessorDefinitions));
            }
        }

        private ProjectProperties GetProjectData()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var customSettings = SettingsManager.Instance.Settings;
            if (customSettings == null || customSettings.AutomaticExtraction)
            {
                switch(EditorUtils.GetEditorMode())
                {
                    case EditorUtils.EditorMode.VisualStudio: return GetProjectDataVisualStudio();
                    case EditorUtils.EditorMode.CMake:        return GetProjectDataCMake();
                    default: return null;
                }
            }
            else
            {
                return GetProjectDataManual();
            }
        }

        private void AddCustomSettings(ProjectProperties projProperties, IMacroEvaluator evaluator)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            SolutionSettings customSettings = SettingsManager.Instance.Settings;
            if (customSettings != null)
            {
                var evaluatorExtra = new MacroEvaluatorExtra();
                AppendMSBuildStringToList(projProperties.IncludeDirectories, evaluator.Evaluate(evaluatorExtra.Evaluate(customSettings.AdditionalIncludeDirs)));
                AppendMSBuildStringToList(projProperties.ForceIncludes, evaluator.Evaluate(evaluatorExtra.Evaluate(customSettings.AdditionalForceIncludes)));
                AppendMSBuildStringToList(projProperties.PrepocessorDefinitions, evaluator.Evaluate(evaluatorExtra.Evaluate(customSettings.AdditionalPreprocessorDefinitions)));
                projProperties.ExtraArguments = evaluator.Evaluate(evaluatorExtra.Evaluate(customSettings.AdditionalCommandLine));
                projProperties.ShowWarnings = customSettings.EnableWarnings;
            }
        }

        private ProjectProperties GetProjectDataVisualStudio()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            OutputLog.Log("Capturing configuration from VS projects...");

            Project project = EditorUtils.GetActiveProject();

            VCProject prj = project.Object as VCProject;
            if (prj == null) return null;

            VCConfiguration config = prj.ActiveConfiguration;
            if (config == null) return null;

            VCPlatform platform = config.Platform;
            if (platform == null) return null;

            var vctools = config.Tools as IVCCollection;
            if (vctools == null) return null;

            var midl = vctools.Item("VCMidlTool") as VCMidlTool;

            var evaluator = new MacroEvaluatorVisualPlatform(platform);

            ProjectProperties ret = new ProjectProperties();
            ret.Target = midl != null && midl.TargetEnvironment == midlTargetEnvironment.midlTargetWin32 ? ProjectProperties.TargetType.x86 : ProjectProperties.TargetType.x64;
            ret.Standard = GetStandardVersion(config);

            //Working directory (always local to processed file)
            ret.WorkingDirectory = Path.GetDirectoryName(project.FullName);

            //Include dirs / files and preprocessor
            AppendMSBuildStringToList(ret.IncludeDirectories, evaluator.Evaluate(platform.IncludeDirectories));
            AppendProjectProperties(ret, vctools.Item("VCCLCompilerTool") as VCCLCompilerTool, vctools.Item("VCNMakeTool") as VCNMakeTool, evaluator);

            //Get settings from the single file (this might fail badly if there are no settings to catpure)
            var applicationObject = EditorUtils.ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            Assumes.Present(applicationObject);
            ProjectItem item = applicationObject.ActiveDocument.ProjectItem;
            VCFile vcfile = item != null? item.Object as VCFile : null;
            IVCCollection fileCfgs = vcfile != null? (IVCCollection)vcfile.FileConfigurations : null;
            VCFileConfiguration fileConfig = fileCfgs != null? fileCfgs.Item(config.Name) as VCFileConfiguration : null;
            VCCLCompilerTool fileToolCL = null;
            VCNMakeTool fileToolNMake = null;

            try 
            {
                fileToolCL = fileConfig.Tool as VCCLCompilerTool;
                fileToolNMake = fileConfig.Tool as VCNMakeTool;
            } 
            catch (Exception e) 
            { 
                //If we really need this data we can always parse the vcxproj as an xml 
                OutputLog.Log("File specific properties not found, only project properties used ("+e.Message+")"); 
            }

            AppendProjectProperties(ret, fileToolCL, fileToolNMake, evaluator);

            AddCustomSettings(ret, evaluator);

            RemoveMSBuildStringFromList(ret.IncludeDirectories, evaluator.Evaluate(platform.ExcludeDirectories)); //Exclude directories 

            return ret;
        }

        private ProjectProperties GetProjectDataCMake()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            OutputLog.Log("Capturing configuration from CMake...");

            var evaluator = new MacroEvaluatorBasic();

            var ret = new ProjectProperties();

            //TODO ~ ramonv ~ to be implemented
            //TODO ~ ramonv ~ retrieve the CMake Commands and parse if for closest setup and command line parameter extraction
            OutputLog.Log("WIP! default to manual configuration.");

            AddCustomSettings(ret, evaluator);

            return ret;
        }

        private ProjectProperties GetProjectDataManual()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            OutputLog.Log("Using manual configuration...");

            var ret = new ProjectProperties();

            var evaluator = new MacroEvaluatorBasic();
            AddCustomSettings(ret, evaluator);

            return ret;
        }
        public void DisplayDialogResult(ParseResult.StatusCode status)
        {
            switch(status)
            {
                case ParseResult.StatusCode.InvalidInput: MessageBox.Show("Parser had Invalid Input.","Struct Layout Result"); break;
                case ParseResult.StatusCode.ParseFailed:  MessageBox.Show("Errors found while parsing.\nCheck the 'Struct Layout' output pane for more information.\nUpdate the Extension's options as needed for a succesful compilation.", "Struct Layout Result"); break;
                case ParseResult.StatusCode.NotFound:     MessageBox.Show("No structure found at the given position.", "Struct Layout Result"); break;
            }
        }

        public async System.Threading.Tasks.Task ParseAtCurrentLocationAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            OutputLog.Clear();

            EditorUtils.SaveActiveDocument();

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

            GeneralSettingsPageGrid settings = EditorUtils.GetGeneralSettings();
            parser.PrintCommandLine = settings.OptionParserShowCommandLine;

            //TODO ~ ramonv ~ add parsing queue to avoid multiple queries at the same time
           
            LayoutWindow prewin = EditorUtils.GetLayoutWindow(false);
            if (prewin != null) 
            { 
                prewin.SetProcessing();
            } 

            var result = await parser.ParseAsync(properties, location);

            DisplayDialogResult(result.Status);

            //Only create or focus the window if we have a valid result
            LayoutWindow win = EditorUtils.GetLayoutWindow(result.Status == ParseResult.StatusCode.Found);
            if (win != null)
            {
                win.SetResult(result);

                if (result.Status == ParseResult.StatusCode.Found)
                {
                    EditorUtils.FocusWindow(win);
                }
            }
        }
    }
}

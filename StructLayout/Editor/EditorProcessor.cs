using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.VCProjectEngine;
using Newtonsoft.Json;
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

        class CMakeCommandEntry
        {
            public string directory { set; get; } 
            public string command { set; get; }
            public string file { set; get; }
        }

        private List<string> ParseCommands(string input)
        {
            List<string> ret = new List<string>();

            bool inQuotes = false;
            string current = "";

            foreach (char c in input)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        ret.Add(current);
                        current = "";
                    }
                }
                else
                {
                    current += c;
                }
            }

            if (current.Length>0)
            {
                ret.Add(current);
            }

            return ret;
        }

        private void ExtractCMakeProjectProperties(ProjectProperties inout, CMakeCommandEntry command)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (command == null) return;

            OutputLog.Log("Extracting commands from Translation Unit: " + command.file);

            inout.WorkingDirectory = command.directory;

            List<string> commands = ParseCommands(command.command);

            for (int i=0;i<commands.Count;++i)
            {
                string com = commands[i];

                if (com.Length > 2 && com[0] == '-' || com[0] == '/')
                {
                    if (com[1] == 'D')
                    {
                        inout.PrepocessorDefinitions.Add(com.Substring(2, com.Length - 2));
                    }
                    else if (com[1] == 'I')
                    {
                        inout.IncludeDirectories.Add(com.Substring(2, com.Length - 2));
                    }
                    else if (com == "-m32")
                    {
                        inout.Target = ProjectProperties.TargetType.x86;
                    }
                    else if (com == "-m64")
                    {
                        inout.Target = ProjectProperties.TargetType.x64;
                    }
                    else if (com.StartsWith("-std"))
                    {
                        string standard = com.Substring(5, com.Length - 5);
                             if (standard == "c++14") inout.Standard = ProjectProperties.StandardVersion.Cpp14;
                        else if (standard == "c++17") inout.Standard = ProjectProperties.StandardVersion.Cpp17;
                        else if (standard == "c++2a") inout.Standard = ProjectProperties.StandardVersion.Latest;
                    }
                    else if (com.StartsWith("-include"))
                    {
                        inout.IncludeDirectories.Add(com.Substring(8, com.Length - 8));
                    }                    
                }
            }
        }

        private int PathCompareScore(string a, string b)
        {
            int ret = 0;
            if (a != null && b != null)
            {
                int max = Math.Min(a.Length, b.Length);
                for (; ret < max && a[ret] == b[ret]; ++ret) { }
            }

            return ret;
        }

        private CMakeCommandEntry FindBestCMakeEntry(List<CMakeCommandEntry> commands, string documentName)
        {
            if (commands == null || documentName == null){ return null; }

            string documentNameNoExt = Path.ChangeExtension(documentName, "");
            string lowerInput = documentNameNoExt.Replace('\\','/').ToLower();
            int bestScoreMatch = 0;
            CMakeCommandEntry bestEntry = null;
            //int bestScoreExtra = Int32.MaxValue;

            foreach(CMakeCommandEntry entry in commands)
            {
                //This assumes no ../../ in those file and documentName paths
                int scoreMatch = PathCompareScore(lowerInput, entry.file.Replace('\\', '/').ToLower());
                if (scoreMatch == lowerInput.Length && (lowerInput.Length == entry.file.Length))
                {
                    //perfect match - early exit
                    return entry;
                }
                else if (scoreMatch >= bestScoreMatch)
                {
                    //TODO ~ ramonv ~ consider nested folders ( get the closest ) - This scoring can be way better

                    bestScoreMatch = scoreMatch;
                    bestEntry = entry;
                }
            }

            return bestEntry;
        }

        private ProjectProperties CaptureCMakeCommands(string commandsFile, string documentName)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var ret = new ProjectProperties();

            if (commandsFile == null || commandsFile.Length == 0)
            {
                OutputLog.Log("Unable to retrieve a CMake commands file.\nMake sure the cmake flag -DCMAKE_EXPORT_COMPILE_COMMANDS=1 is set and the options are pointing to the proper file.");
            }
            else
            {
                if (File.Exists(commandsFile))
                {
                    OutputLog.Log("Capturing commands from CMake commands file: " + commandsFile);

                    List<CMakeCommandEntry> allCommands = null;
                    try
                    {
                        string jsonString = File.ReadAllText(commandsFile);
                        allCommands = JsonConvert.DeserializeObject<List<CMakeCommandEntry>>(jsonString);
                    }
                    catch (Exception e)
                    {
                        OutputLog.Error(e.Message);
                    }

                    ExtractCMakeProjectProperties(ret, FindBestCMakeEntry(allCommands, documentName));
                }
                else
                {
                    OutputLog.Log("Unable to find CMake commands file at " + commandsFile + ".\nMake sure the cmake flag -DCMAKE_EXPORT_COMPILE_COMMANDS=1 is set and the options are pointing to the proper file.");
                }
            }

            return ret; 
        }

        private ProjectProperties GetProjectDataCMake()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            OutputLog.Log("Capturing configuration from CMake...");

            Document document = EditorUtils.GetActiveDocument();
            if (document == null) { return null; }

            var evaluator = new MacroEvaluatorBasic();

            var customSettings = SettingsManager.Instance.Settings;
            string commandsFile = customSettings == null ? null : customSettings.CMakeCommandsFile;
            commandsFile = commandsFile == null ? null : evaluator.Evaluate(commandsFile);

            var ret = CaptureCMakeCommands(commandsFile, document.FullName);

            AddCustomSettings(ret, evaluator);

            return ret;
        }

        private ProjectProperties GetProjectDataManual()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            OutputLog.Log("Using manual configuration...");

            var ret = new ProjectProperties();

            Project project = EditorUtils.GetActiveProject();
            VCProject prj = project == null? null : project.Object as VCProject;
            VCConfiguration config = prj == null? null : prj.ActiveConfiguration;
            VCPlatform platform = config == null? null : config.Platform;

            if (platform != null)
            {
                AddCustomSettings(ret, new MacroEvaluatorVisualPlatform(platform));
            }
            else
            {
                AddCustomSettings(ret, new MacroEvaluatorBasic());
            }

            return ret;
        }
     
        private void ApplyUserSettingsToWindow(LayoutWindow window, GeneralSettingsPageGrid settings)
        {
            if (window == null || settings == null) return;
            window.SetGridNumberBase(settings.OptionViewerGridBase);
        }

        public void OnUserSettingsChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GeneralSettingsPageGrid settings = EditorUtils.GetGeneralSettings();
            LayoutWindow win = EditorUtils.GetLayoutWindow(false);
            ApplyUserSettingsToWindow(win, settings);
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

            ParseResult result = await parser.ParseAsync(properties, location);

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

            ParseMessageWindow.DisplayResult(result);
        }
    }
}

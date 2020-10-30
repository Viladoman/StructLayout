﻿using EnvDTE;
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

            //Get full file path
            var applicationObject = EditorUtils.ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            Assumes.Present(applicationObject);

            string filename = applicationObject.ActiveDocument.FullName;

            //Get text location
            var textManager = EditorUtils.ServiceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            if (textManager == null) return null;

            IVsTextView view;
            textManager.GetActiveView2(1, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out view);
            if (view == null) return null;

            view.GetCaretPos(out int line, out int col);

            return new DocumentLocation(filename,(uint)(line+1),(uint)(col+1));
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

            switch(EditorUtils.GetEditorMode())
            {
                case EditorUtils.EditorMode.VisualStudio: return GetProjectDataVisualStudio();
                case EditorUtils.EditorMode.CMake:        return GetProjectDataCMake();
                default: return null;
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

            ProjectProperties ret = new ProjectProperties();
            ret.Target = midl != null && midl.TargetEnvironment == midlTargetEnvironment.midlTargetWin32 ? ProjectProperties.TargetType.x86 : ProjectProperties.TargetType.x64;
            ret.Standard = GetStandardVersion(config);

            //Working directory (always local to processed file)
            ret.WorkingDirectory = Path.GetDirectoryName(project.FullName);

            //Include dirs / files and preprocessor
            AppendMSBuildStringToList(ret.IncludeDirectories, platform.Evaluate(platform.IncludeDirectories));
            AppendProjectProperties(ret, vctools.Item("VCCLCompilerTool") as VCCLCompilerTool, vctools.Item("VCNMakeTool") as VCNMakeTool, platform);

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
                //TODO ~ ramonv ~ get the data from the vcxproj XML ( if enabled parse the solution folder ) 
                OutputLog.Log("File specific properties not found, only project properties used ("+e.Message+")"); 
            }

            AppendProjectProperties(ret, fileToolCL, fileToolNMake, platform);

            //TODO ~ ramonv ~ move toa different function for reusage between VS and CMAKE
            SolutionSettings customSettings = SettingsManager.Instance.Settings;
            if (customSettings != null)
            {
                AppendMSBuildStringToList(ret.IncludeDirectories, platform.Evaluate(customSettings.AdditionalIncludeDirs));
                AppendMSBuildStringToList(ret.ForceIncludes, platform.Evaluate(customSettings.AdditionalForceIncludes));
                AppendMSBuildStringToList(ret.PrepocessorDefinitions, platform.Evaluate(customSettings.AdditionalPreprocessorDefinitions));
                ret.ExtraArguments = platform.Evaluate(customSettings.AdditionalCommandLine);
                ret.ShowWarnings = customSettings.EnableWarnings;
            }

            //Exclude directories 
            RemoveMSBuildStringFromList(ret.IncludeDirectories, platform.Evaluate(platform.ExcludeDirectories));

            return ret;
        }

        private ProjectProperties GetProjectDataCMake()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            OutputLog.Log("Capturing configuration from CMake...");

            //TODO ~ ramonv ~ to be implemented

            return null;
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

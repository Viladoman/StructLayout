using EnvDTE;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StructLayout
{
    public class ExtractorVisualStudio : IExtractor
    {
        public override ProjectProperties GetProjectData()
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
            VCFile vcfile = item != null ? item.Object as VCFile : null;
            IVCCollection fileCfgs = vcfile != null ? (IVCCollection)vcfile.FileConfigurations : null;
            VCFileConfiguration fileConfig = fileCfgs != null ? fileCfgs.Item(config.Name) as VCFileConfiguration : null;
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
                OutputLog.Log("File specific properties not found, only project properties used (" + e.Message + ")");
            }

            AppendProjectProperties(ret, fileToolCL, fileToolNMake, evaluator);

            CaptureExtraProperties(ret, evaluator);

            AddCustomSettings(ret, evaluator);

            RemoveMSBuildStringFromList(ret.IncludeDirectories, evaluator.Evaluate(platform.ExcludeDirectories)); //Exclude directories 

            ProcessPostProjectData(ret);

            return ret;
        }

        protected virtual void CaptureExtraProperties(ProjectProperties projProperties, IMacroEvaluator evaluator){ }
        protected virtual void ProcessPostProjectData(ProjectProperties projProperties) { }

        private ProjectProperties.StandardVersion GetStandardVersion(VCConfiguration config)
        {
            IVCRulePropertyStorage generalRule = config.Rules.Item("ConfigurationGeneral");
            string value = null;

            try { value = generalRule == null ? null : generalRule.GetEvaluatedPropertyValue("LanguageStandard"); } catch (Exception) { }

            if (value == "Default") { return ProjectProperties.StandardVersion.Default; }
            else if (value == "stdcpp14") { return ProjectProperties.StandardVersion.Cpp14; }
            else if (value == "stdcpp17") { return ProjectProperties.StandardVersion.Cpp17; }
            else if (value == "stdcpplatest") { return ProjectProperties.StandardVersion.Latest; }

            return ProjectProperties.StandardVersion.Latest;
        }

        private void AppendProjectProperties(ProjectProperties properties, VCCLCompilerTool cl, VCNMakeTool nmake, IMacroEvaluator evaluator)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (cl != null)
            {
                AppendMSBuildStringToList(properties.IncludeDirectories, evaluator.Evaluate(cl.FullIncludePath));
                AppendMSBuildStringToList(properties.ForceIncludes, evaluator.Evaluate(cl.ForcedIncludeFiles));
                AppendMSBuildStringToList(properties.PrepocessorDefinitions, evaluator.Evaluate(cl.PreprocessorDefinitions));
            }
            else if (nmake != null)
            {
                AppendMSBuildStringToList(properties.IncludeDirectories, evaluator.Evaluate(nmake.IncludeSearchPath));
                AppendMSBuildStringToList(properties.ForceIncludes, evaluator.Evaluate(nmake.ForcedIncludes));
                AppendMSBuildStringToList(properties.PrepocessorDefinitions, evaluator.Evaluate(nmake.PreprocessorDefinitions));
            }
        }
    }
}

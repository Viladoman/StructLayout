using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StructLayout
{
    public abstract class IExtractor 
    {
        public abstract ProjectProperties GetProjectData();

        protected static void AppendMSBuildStringToList(List<string> list, string input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var split = input.Split(';').ToList(); //Split
            split.RemoveAll(s => IsMSBuildStringInvalid(s)); //Validate

            foreach (string str in split)
            {
                string trimmedStr = str.Trim();
                if (!list.Contains(trimmedStr))
                {
                    list.Add(trimmedStr);
                }
            }
        }

        protected static void RemoveMSBuildStringFromList(List<string> list, string input)
        {
            var split = input.Split(';').ToList(); //Split

            foreach (string str in split)
            {
                list.Remove(str.Trim());
            }
        }

        private static bool StringHasContent(string input)
        {
            foreach (char c in input)
            {
                if ( c != ' ' && c != '"' && c != '\\' && c != '/')
                {
                    return true;
                }
            }

            return false;
        } 

        protected static bool IsMSBuildStringInvalid(string input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();     

            if (!StringHasContent(input))
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

        protected void AddCustomSettings(ProjectProperties projProperties, IMacroEvaluator evaluator)
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
    }
}

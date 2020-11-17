using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace StructLayout
{
    interface IMacroEvaluator
    {
        string Evaluate(string input);
    }

    public class MacroEvaluatorVisualPlatform : IMacroEvaluator
    {
        private VCPlatform Platform { set; get; }

        public MacroEvaluatorVisualPlatform(VCPlatform platform)
        {
            Platform = platform;
        }

        public string Evaluate(string input)
        {
            return Platform.Evaluate(input);
        }
    }

    public abstract class MacroEvaluatorDict : IMacroEvaluator
    {
        private Dictionary<string, string> dict = new Dictionary<string, string>();

        public abstract string ComputeMacro(string macroStr);      

        public string Evaluate(string input)
        {
            return Regex.Replace(input, @"(\$\([a-zA-Z0-9_]+\))", delegate (Match m)
            {                
                if (dict.ContainsKey(m.Value))
                {
                    return dict[m.Value];
                }

                string macroValue = ComputeMacro(m.Value);

                if (macroValue != null)
                {
                    dict[m.Value] = macroValue;
                    return macroValue;
                }

                return m.Value;
            });
        }
    }

    public class MacroEvaluatorCMake : MacroEvaluatorDict
    {
        public override string ComputeMacro(string macroStr)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (macroStr == @"$(SolutionDir)")
            {
                return EditorUtils.GetSolutionPath();
            }
            if (macroStr == @"$(Configuration)")
            {
                return EditorUtils.GetCMakeActiveConfigurationName();
            }

            return null;
        }
    }

    public class MacroEvaluatorExtra : MacroEvaluatorDict
    {
        public override string ComputeMacro(string macroStr)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (macroStr == @"$(UE4ModuleName)")
            {
                Document doc = EditorUtils.GetActiveDocument();
                if (doc == null)
                {
                    return null;
                }
                
                string path = doc.FullName;
                var dirInfo = Directory.GetParent(path);

                while (dirInfo != null)
                {
                    path = dirInfo.FullName;

                    string folderName = Path.GetFileName(path);
                    if (File.Exists(path+"\\"+folderName+".Build.cs"))
                    {
                        OutputLog.Log("UE4 Module Name: " + folderName);
                        return folderName;
                    }

                    dirInfo = Directory.GetParent(path); //Next folder
                }

                OutputLog.Log("Unable to find the UE4 Module Name");
            }

            return null;
        }
    }
}

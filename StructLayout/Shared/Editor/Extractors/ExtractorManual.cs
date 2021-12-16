﻿using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StructLayout
{
    public class ExtractorManual : IExtractor
    {
        public override ProjectProperties GetProjectData()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            OutputLog.Log("Using manual configuration...");

            var ret = new ProjectProperties();

            Project project = EditorUtils.GetActiveProject();
            VCProject prj = project == null ? null : project.Object as VCProject;
            VCConfiguration config = prj == null ? null : prj.ActiveConfiguration;
            VCPlatform platform = config == null ? null : config.Platform as VCPlatform;

            if (platform != null)
            {
                AddCustomSettings(ret, new MacroEvaluatorVisualPlatform(platform));
            }
            else
            {
                AddCustomSettings(ret, new MacroEvaluatorCMake());
            }

            return ret;
        }

        public override string GetPDBPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            return EvaluateMacros(SettingsManager.Instance.Settings.PDBLocation);
        }

        public override string EvaluateMacros(string input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Project project = EditorUtils.GetActiveProject();
            VCProject prj = project.Object as VCProject;
            VCConfiguration config = prj == null ? null : prj.ActiveConfiguration;
            VCPlatform platform = config == null ? null : config.Platform as VCPlatform;

            var evaluatorExtra = new MacroEvaluatorExtra();
            string output = evaluatorExtra.Evaluate(input);

            if (platform != null)
            {
                var evaluatorPlatform = new MacroEvaluatorVisualPlatform(platform);
                output = evaluatorPlatform.Evaluate(output);
            }
            else
            {
                var evaluatorPlatform = new MacroEvaluatorCMake();
                output = evaluatorPlatform.Evaluate(output);
            }

            return output;
        }

    }
}

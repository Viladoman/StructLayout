using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.VCProjectEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace StructLayout
{
    public class EditorPosition
    {
        public EditorPosition(string filename, uint line, uint column)
        {
            Filename = filename;
            Line = line;
            Column = column;
        }

        public string Filename { get; }
        public uint    Line { get; }
        public uint    Column { get; }
    };

    public class ProjectProperties
    {
        public enum TargetType
        {
            x86,
            x64,
        }

        public string IncludeDirectories { set; get; }
        public string PrepocessorDefinitions { set; get; }
        public TargetType Target { set; get; } 

        //Add architecture
    }

    public class EditorProcessor
    {
        static public EditorPosition GetCurrentPosition(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //Get full file path
            var applicationObject = serviceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            if (applicationObject == null) return null;

            string filename = applicationObject.ActiveDocument.FullName;

            //Get text location
            var textManager = serviceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            if (textManager == null) return null;

            IVsTextView view;
            textManager.GetActiveView2(1, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out view);

            view.GetCaretPos(out int line, out int col);

            return new EditorPosition(filename,(uint)line,(uint)col);
        }

        static public Project GetActiveProject(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var applicationObject = serviceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            if (applicationObject == null || applicationObject.ActiveDocument == null || applicationObject.ActiveDocument.ProjectItem == null) return null;
            return applicationObject.ActiveDocument.ProjectItem.ContainingProject;
        }
        /*
        static void Test(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
      
            Project project = GetActiveProject(serviceProvider);
            if (project != null)
            {
                OutputLog.Log(project.Name);
                OutputLog.Log("----------");
                foreach (Property property in project.Properties)
                {
                    //Check for ActiveConfiguration... 
                    //Try to extract MACROS & Include Paths & x64/86

                    OutputLog.Log("\t"+property.Name);

                    foreach (Property property2 in property.Collection)
                    {
                        OutputLog.Log("\t- " + property2.Name);
                    }

                }
            }


            //configuration

            var dte = serviceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            
            ThreadHelper.ThrowIfNotOnUIThread();

            ConfigurationManager configmgr;
            Configuration config;
            if (dte.Solution.Projects.Count > 0)
            {
                configmgr = dte.Solution.Projects.Item(1).ConfigurationManager;
                // Return the ActiveConfiguration.  
                config = configmgr.ActiveConfiguration;

                

                OutputLog.Log(config.PlatformName);
            }


            //Add ifs all the way 
            Project project = GetActiveProject(serviceProvider);
            VCProject prj = project.Object as VCProject;
            VCConfiguration config = prj.ActiveConfiguration;

    
            IVCRulePropertyStorage generalRule = config.Rules.Item("ConfigurationGeneral");

            string outputPath = config.OutputDirectory;

            //vccon.OutputDirectory = "$(test)";
            //string test1 = generalRule.GetEvaluatedPropertyValue(2);
            string tar = generalRule.GetEvaluatedPropertyValue("TargetExt");
            string name = generalRule.GetEvaluatedPropertyValue("TargetName");
         

            var vctools = config.Tools as IVCCollection;
            var midl = vctools.Item("VCMidlTool") as VCMidlTool;
            var cl = vctools.Item("VCCLCompilerTool") as VCCLCompilerTool;
            var nmake = vctools.Item("VCNMakeTool") as VCNMakeTool;

            if (midl != null)
            {
                midlTargetEnvironment environment = midl.TargetEnvironment; //gcc uses -m32 ( check for clang ) 
            }

            if (cl != null)
            {
                string includeDirs = cl.AdditionalIncludeDirectories;
                string preprocessor = cl.PreprocessorDefinitions;
            }
            else if (nmake != null)
            {
                string preprocessor2 = nmake.PreprocessorDefinitions;
                string includes2 = nmake.IncludeSearchPath;
            }

            //parse and replace include macros ${SolutionDir} ...


            //IVCRulePropertyStorage generalRule = config.Rules.Item("ConfigurationGeneral");



            //VCProject prj = dte.Solution.Projects.Item(1).Object as VCProject;
            foreach (VCConfiguration vccon in prj.Configurations)
            {
                IVCRulePropertyStorage generalRule = vccon.Rules.Item("ConfigurationGeneral");

                string outputPath = vccon.OutputDirectory;

                vccon.OutputDirectory = "$(test)";
                //string test1 = generalRule.GetEvaluatedPropertyValue(2);
                string tar = generalRule.GetEvaluatedPropertyValue("TargetExt");
                string name = generalRule.GetEvaluatedPropertyValue("TargetName");
            }
            

        }
        */

        static public ProjectProperties GetProjectData(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //TODO ~ ramonv ~ Add ifs all the way 
            Project project = GetActiveProject(serviceProvider);
            VCProject prj = project.Object as VCProject;
            VCConfiguration config = prj.ActiveConfiguration;

            var vctools = config.Tools as IVCCollection;
            var midl = vctools.Item("VCMidlTool") as VCMidlTool;
            var cl = vctools.Item("VCCLCompilerTool") as VCCLCompilerTool;
            var nmake = vctools.Item("VCNMakeTool") as VCNMakeTool;

            if (cl == null && nmake == null)
            {
                return null;
            }

            ProjectProperties ret = new ProjectProperties();
            ret.Target = midl != null && midl.TargetEnvironment == midlTargetEnvironment.midlTargetWin32 ? ProjectProperties.TargetType.x86 : ProjectProperties.TargetType.x64;

            if (cl != null)
            {
                ret.IncludeDirectories = cl.AdditionalIncludeDirectories; //TODO ~ ramonv ~ parse ${macros}
                ret.PrepocessorDefinitions = cl.PreprocessorDefinitions;
            }
            else
            {
                ret.IncludeDirectories = nmake.IncludeSearchPath; //TODO ~ ramonv ~ parse ${macros}
                ret.PrepocessorDefinitions = nmake.PreprocessorDefinitions;
            }

            return ret;
        }


        static public void ParseAtCurrentLocation(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            EditorPosition position = GetCurrentPosition(serviceProvider);

            if (position == null)
            {
                OutputLog.Error("Unable to retrieve current document position.");
                return;
            }

            ProjectProperties properties = GetProjectData(serviceProvider);

            if (properties == null)
            {
                OutputLog.Error("Unable to retrieve the project configuration");
                return;
            } 

            OutputLog.Log(position.Filename + " " + position.Line + " " + position.Column);

            //PLACEHOLDER - move to its own place 

            string archStr = properties != null && properties.Target == ProjectProperties.TargetType.x86 ? "-m32" : "-m64";

            //TODO ~ ramonv ~ passi includes and macros

            string toolCmd = "--show " + position.Filename + " -- clang++ -x c++ " + archStr;

            if (ParseLocation(toolCmd, position.Filename, position.Line+1, position.Column+1))
            {
                uint size = 0;
                IntPtr result = GetData(ref size);
                byte[] managedArray = new byte[size];
                Marshal.Copy(result, managedArray, 0, (int)size);

                Clear();
                
                MessageBox.Show("This is a struct");
            }
            else 
            {
                MessageBox.Show("NOTHING!!!!");
            }
        }

        //move to layoutParser... for testing purposes only

        [DllImport("LayoutParser.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool ParseLocation(string commandline, string fullFilename, uint row, uint col);

        [DllImport("LayoutParser.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool ParseType(string commandline, string typeName);

        [DllImport("LayoutParser.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetData(ref uint size);

        [DllImport("LayoutParser.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Clear();
    }
}

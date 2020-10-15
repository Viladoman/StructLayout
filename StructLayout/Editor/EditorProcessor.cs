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

        public DocumentLocation GetCurrentLocation()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //Get full file path
            var applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            if (applicationObject == null) return null;

            string filename = applicationObject.ActiveDocument.FullName;

            //Get text location
            var textManager = ServiceProvider.GetService(typeof(SVsTextManager)) as IVsTextManager2;
            if (textManager == null) return null;

            IVsTextView view;
            textManager.GetActiveView2(1, null, (uint)_VIEWFRAMETYPE.vftCodeWindow, out view);

            view.GetCaretPos(out int line, out int col);

            return new DocumentLocation(filename,(uint)line,(uint)col);
        }

        public Project GetActiveProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            if (applicationObject == null || applicationObject.ActiveDocument == null || applicationObject.ActiveDocument.ProjectItem == null) return null;
            return applicationObject.ActiveDocument.ProjectItem.ContainingProject;
        }
       
        public ProjectProperties GetProjectData()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //TODO ~ ramonv ~ Add ifs all the way 
            Project project = GetActiveProject();
            VCProject prj = project.Object as VCProject;
            VCConfiguration config = prj.ActiveConfiguration;

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

            if (cl != null)
            {
                ret.IncludeDirectories = cl.AdditionalIncludeDirectories; //TODO ~ ramonv ~ parse ${macros}
                ret.PrepocessorDefinitions = cl.PreprocessorDefinitions; //split
            }
            else
            {
                ret.IncludeDirectories = nmake.IncludeSearchPath; //TODO ~ ramonv ~ parse ${macros} and split
                ret.PrepocessorDefinitions = nmake.PreprocessorDefinitions; //split
            }

            return ret;
        }

        public void DisplayLayout(LayoutNode node)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            LayoutWindow window = FocusLayoutWindow();
            window.SetLayout(node);
        }

        public LayoutWindow FocusLayoutWindow()
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

            window.ProxyShow();

            return window;
        }

        public void ParseAtCurrentLocation()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

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

            DisplayLayout(parser.Parse(properties, location));
        }
    }
}

﻿using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.IO;

namespace StructLayout
{
    static public class EditorUtils
    {
        public enum EditorMode
        {
            None, 
            VisualStudio,
            CMake,
            UnrealEngine,
        }

        static private StructLayoutPackage Package { get; set; }
        static public IServiceProvider ServiceProvider { get; set; }

        static public void Initialize(StructLayoutPackage package)
        {
            Package = package;
            ServiceProvider = package;
        }

        static public GeneralSettingsPageGrid GetGeneralSettings()
        {
            return (GeneralSettingsPageGrid)Package.GetDialogPage(typeof(GeneralSettingsPageGrid));
        }

        static public Document GetActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var applicationObject = EditorUtils.ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            Assumes.Present(applicationObject);
            return applicationObject.ActiveDocument;
        }

        static public Project GetActiveProject()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            Assumes.Present(applicationObject);
            if (applicationObject.ActiveDocument == null || applicationObject.ActiveDocument.ProjectItem == null) return null;
            return applicationObject.ActiveDocument.ProjectItem.ContainingProject;
        }

        static public Solution GetActiveSolution()
        {
            DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
            Assumes.Present(applicationObject);
            return applicationObject.Solution;
        }

        static public string GetSolutionPath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Solution solution = GetActiveSolution();
            if (solution == null) return null;
            return (Path.HasExtension(solution.FullName) ? Path.GetDirectoryName(solution.FullName) : solution.FullName) + '\\';
        }

        static public EditorMode GetEditorMode()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Project project = EditorUtils.GetActiveProject();
            if (project == null)
            {
                return EditorMode.None;
            } 

            if (project.Object == null)
            {
                return EditorMode.CMake;
            }

            Solution solution = EditorUtils.GetActiveSolution();
            if (solution != null)
            {
                //Check for unreal project ( $(SolutionName.uproject) || UE4.sln + Engine/Source/UE4Editor.target )
                var uproject = Path.ChangeExtension(solution.FullName, "uproject");
                if (File.Exists(uproject))
                {
                    return EditorMode.UnrealEngine;
                }
                else if (Path.GetFileNameWithoutExtension(solution.FullName) == "UE4" && File.Exists(GetSolutionPath() + @"Engine/Source/UE4Editor.Target.cs"))
                {
                    return EditorMode.UnrealEngine;
                }
                else if (Path.GetFileNameWithoutExtension(solution.FullName) == "UE5" && File.Exists(GetSolutionPath() + @"Engine/Source/UnrealEditor.Target.cs"))
                {
                    return EditorMode.UnrealEngine;
                }
            }

            return EditorMode.VisualStudio;
        }

        static public void SaveActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //Get full file path
            var applicationObject = ServiceProvider.GetService(typeof(DTE)) as EnvDTE80.DTE2;
            Assumes.Present(applicationObject);

            Document doc = applicationObject.ActiveDocument;
            if (doc != null && !doc.ReadOnly && !doc.Saved)
            {
                doc.Save();
            }
        }

        static public LayoutWindow GetLayoutWindow(bool create = true)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Get the instance number 0 of this tool window. This window is single instance so this instance
            // is actually the only one.
            // The last flag is set to true so that if the tool window does not exists it will be created.
            LayoutWindow window = Package.FindToolWindow(typeof(LayoutWindow), 0, create) as LayoutWindow;
            if ((null == window) || (null == window.GetFrame()))
            {
                return null;
            }

            return window;
        }

        static public void FocusWindow(LayoutWindow window)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (window != null)
            {
                window.ProxyShow();
            }
        }
    }
}

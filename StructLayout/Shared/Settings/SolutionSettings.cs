using EnvDTE;
using EnvDTE80;
using Microsoft;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Newtonsoft.Json;
using System;
using System.IO;

namespace StructLayout
{
    [AttributeUsage(AttributeTargets.Property)]
    public class UIDescription : Attribute
    {
        public string Label { get; set; }
        public string Tooltip { get; set; }
        public string FilterMethod { get; set; }
    }

    static public class UISettingsFilters
    {
        static public bool IsClangParser(SolutionSettings input)
        {
            return input.ExtractionTool == EditorProcessor.ParserTool.Clang;
        }

        static public bool IsPDBParser(SolutionSettings input)
        {
            return input.ExtractionTool == EditorProcessor.ParserTool.PDB;
        }
        
        static public bool DisplayCMakeCommandsFile(SolutionSettings input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return IsClangParser(input) && EditorUtils.GetEditorMode() == EditorUtils.EditorMode.CMake;
        }

        static public bool DisplayPDBPath(SolutionSettings input)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            return IsPDBParser(input) && (!input.AutomaticExtraction || EditorUtils.GetEditorMode() != EditorUtils.EditorMode.VisualStudio);
        }
    }

    public class SolutionSettings
    {
        [UIDescription(Label = "Extraction Tool", Tooltip = "The approach used when a layout is queried. Clang: uses a clang frontend compilation to extract the layout, PDB: queries the generated pdb for the layout.")]
        public EditorProcessor.ParserTool ExtractionTool { set; get; } = EditorProcessor.ParserTool.Clang;

        ////////////
        // Common //
        ////////////

        [UIDescription(Label = "Automatic Extraction", Tooltip = "If true, it will try to extract the architecture, include paths, preprocessor macros... from the current solution.")]
        public bool AutomaticExtraction { set; get; } = true;

        ///////////////////////////
        // Clang Parser Settings //
        ///////////////////////////

        [UIDescription(Label = "Explicit Commands File", FilterMethod= "DisplayCMakeCommandsFile" , Tooltip = "File location for the build commands exported by CMAKE_EXPORT_COMPILE_COMMANDS=1 (This fields allows a limited set of $(SolutionDir) style macros)")]
        public string CMakeCommandsFile { set; get; } = "";

        [UIDescription(Label = "Extra Preprocessor Defintions", FilterMethod = "IsClangParser", Tooltip = "Additional preprocessor definitions on top of the auto extracted form the project configuration. (This fields allows $(SolutionDir) style macros)")]
        public string AdditionalPreprocessorDefinitions { set; get; } = "";

        [UIDescription(Label = "Extra Include Dirs", FilterMethod = "IsClangParser", Tooltip = "Additional include directories on top of the auto extracted form the project configuration. (This fields allows $(SolutionDir) style macros)")]
        public string AdditionalIncludeDirs { set; get; } = "";

        [UIDescription(Label = "Extra Force Includes", FilterMethod = "IsClangParser", Tooltip = "Additional files to force include on top of the auto extracted form the project configuration. (This fields allows $(SolutionDir) style macros)")]
        public string AdditionalForceIncludes { set; get; } = "";

        [UIDescription(Label = "Extra Parser Args", FilterMethod = "IsClangParser", Tooltip = "Additional command line arguments passed in to the clang parser. (This fields allows $(SolutionDir) style macros)")]
        public string AdditionalCommandLine { set; get; } = "";

        [UIDescription(Label = "Enable Warnings", FilterMethod = "IsClangParser", Tooltip = "If true, the clang parser will output the warnings found.")]
        public bool EnableWarnings { set; get; } = false;

        /////////////////////////
        // PDB Parser Settings //
        /////////////////////////

        [UIDescription(Label = "PDB Location", FilterMethod = "DisplayPDBPath", Tooltip = "Specific location for the pdb to be parsed.")]
        public string PDBLocation { set; get; } = "";

        ////////////
        // Common //
        ////////////

        [UIDescription(Label = "Parser Output Folder", Tooltip = "File location where the Clang Parser will output the layout results. This files are temporary. This field will default to the extension installation folder. (This fields allows $(SolutionDir) style macros)")]
        public string ParserOutputFolder { set; get; } = "";
    }

    public class SettingsManager
    {
        private static readonly Lazy<SettingsManager> lazy = new Lazy<SettingsManager>(() => new SettingsManager());
        public static SettingsManager Instance { get { return lazy.Value; } }

        const string SettingsName = "StructLayoutSettings.json";

        private SolutionEvents solutionEvents; //Super important if not copied the events get disposed and never triggered
        private DocumentEvents documentEvents; //Super important if not copied the events get disposed and never triggered
        
        public SolutionSettings Settings { get; set; }
        private string Filename{ set; get; }
        private Common.FileWatcher Watcher { set; get; }  = new Common.FileWatcher();

        private IServiceProvider ServiceProvider { set; get; }

        public void Initialize(IServiceProvider serviceProvider)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ServiceProvider = serviceProvider;

            DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
            Assumes.Present(applicationObject);
            solutionEvents = applicationObject.Events.SolutionEvents;
            solutionEvents.AfterClosing += RefreshFilename;
            solutionEvents.Opened += RefreshFilename;

            documentEvents = applicationObject.Events.DocumentEvents;
            documentEvents.DocumentOpened += TryRefreshFilename;

            Watcher.FileWatchedChanged += Load;

            RefreshFilename();
        }

        private void TryRefreshFilename(object dummy = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (Filename == null)
            {
                RefreshFilename();
            }
        }

        private void RefreshFilename()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            string newFilename = null;

            DTE2 applicationObject = ServiceProvider.GetService(typeof(SDTE)) as DTE2;
            if (applicationObject != null && applicationObject.Solution != null)
            {
                string solutionDirRaw = applicationObject.Solution.FullName;
                if (solutionDirRaw.Length > 0)
                {
                    newFilename = (Path.HasExtension(solutionDirRaw) ? Path.GetDirectoryName(solutionDirRaw) : solutionDirRaw) + '/' + SettingsName;
                }
            }

            SetFilename(newFilename);
        }

        private void SetFilename(string str)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (Filename != str)
            {
                Watcher.Unwatch();
                Filename = str;
                Load();
                Watcher.Watch(Filename);
            }
        }

        private void Load()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (Filename != null && File.Exists(Filename))
            {
                try
                {
                    string jsonString = File.ReadAllText(Filename);
                    Settings = JsonConvert.DeserializeObject<SolutionSettings>(jsonString);
                }
                catch(Exception e)
                {
                    OutputLog.Error(e.Message);
                }
            }
        }

        public void Save()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            TryRefreshFilename();

            if (Filename != null && Settings != null)
            {
                try
                {
                    string jsonString = JsonConvert.SerializeObject(Settings, Formatting.Indented);
                    File.WriteAllText(Filename, jsonString);
                }
                catch (Exception e)
                {
                    OutputLog.Error(e.Message);
                }
            }
        }

        private static T CloneJson<T>(T source)
        {
            if (Object.ReferenceEquals(source, null))
            {
                return default(T);
            }

            var deserializeSettings = new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace };
            return JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(source), deserializeSettings);
        }

        public void OpenSettingsWindow()
        {
            SettingsWindow optionsWindow = new SettingsWindow(CloneJson<SolutionSettings>(Settings));
            optionsWindow.ShowDialog();
        }
    }
}

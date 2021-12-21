using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System;
using System.IO;

namespace StructLayout
{
    public class EditorProcessor
    {
        public enum ParserTool
        {
            Clang,
            PDB
        };

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

        private IExtractor GetProjectExtractor()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var customSettings = SettingsManager.Instance.Settings;
            if (customSettings == null || customSettings.AutomaticExtraction)
            {
                switch (EditorUtils.GetEditorMode())
                {
                    case EditorUtils.EditorMode.UnrealEngine:  return new ExtractorUnreal();
                    case EditorUtils.EditorMode.VisualStudio:  return new ExtractorVisualStudio();
                    case EditorUtils.EditorMode.CMake:         return new ExtractorCMake();
                }
            }
            else
            {
                return new ExtractorManual();
            }

            return null;
        }

        private ProjectProperties GetProjectData()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var extractor = GetProjectExtractor();
            return extractor == null? null : extractor.GetProjectData();
        }

        private string GetParserOutputDirectory()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var customSettings = SettingsManager.Instance.Settings;
            string dir = customSettings == null ? null : customSettings.ParserOutputFolder;          

            if (dir == null || dir.Length == 0)
            {
                //default to the extension installation directory
                dir = @"$(ExtensionInstallationDir)Generated";
            }

            dir = GetProjectExtractor().EvaluateMacros(dir);

            if (dir != null && dir.Length > 0)
            {
                //make sure the directory has a proper format
                char lastchar = dir[dir.Length - 1];
                if (lastchar != '\\' && lastchar != '/')
                {
                    dir += '\\';
                }
            }

            return dir;
        }

        private void ApplyUserSettingsToWindow(LayoutWindow window, GeneralSettingsPageGrid settings)
        {
            if (window == null || settings == null) return;
            window.RefreshDefaults();
        }

        public void OnUserSettingsChanged()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GeneralSettingsPageGrid settings = EditorUtils.GetGeneralSettings();
            LayoutWindow win = EditorUtils.GetLayoutWindow(false);
            ApplyUserSettingsToWindow(win, settings);
        }

        private void CheckForSpecialResults(ParseMessageContent content)
        {
            if (content.Log != null && content.Log.Length > 0)
            {
                //Special Issue Messages
                //if (content.Log.Contains("error: use of overloaded operator '==' is ambiguous (with operand types 'const FName' and 'const FPrimaryAssetType')"))
                //{
                    //content.Doc = Documentation.Link.UnrealIssue_1;
                    //content.Message = "Errors found while parsing.\nThis is a known Unreal Engine issue.\nPress the Documentation Button for more details.";
                //}
            }
        }

        private string GetErrorMessage(ParseResult.StatusCode code)
        {
            switch (code)
            {
                case ParseResult.StatusCode.InvalidInput:     return "Parser had Invalid Input.";
                case ParseResult.StatusCode.InvalidLocation:  return "Unable to retrieve current document position.";
                case ParseResult.StatusCode.InvalidProject:   return "Unable to retrieve the project configuration";
                case ParseResult.StatusCode.InvalidOutputDir: return "Unable to create the directory for the generated parser results.\nPlease make sure the parser output directory set in the Extension Options is valid and writable.";
                case ParseResult.StatusCode.InvalidPDB:       return "Unable to find the PDB file.";
                case ParseResult.StatusCode.VersionMismatch:  return "Parser result generated version does not match the current version.";
                case ParseResult.StatusCode.ParseFailed:      return "Errors found while parsing.\nUpdate the Extension's options as needed for a succesful compilation.\nCheck the 'Struct Layout' output pane for more information.";
                case ParseResult.StatusCode.Found:            return null;
                case ParseResult.StatusCode.NotFound:
                    return SettingsManager.Instance.Settings.ExtractionTool == ParserTool.PDB?
                        "No structure found at the given position.\n" +
                        "This might happen by any of the following reasons:\n" + 
                        "- The query wasn't done at the first line of the structure.\n" +
                        "- The PDB is not up to date\n" + 
                        "- The PDB does not have the requested symbol\n"
                        :
                        "No structure found at the given position.\nTry performing the query from a structure definition or initialization.";

                case ParseResult.StatusCode.UnknownTool:      return "The system does not know how to use the provided layout tool.";
                case ParseResult.StatusCode.Unknown:
                default:                                      return "Unkown error. Please contact with the author!";
            }
        } 

        private void DisplayResult(ParseResult result)
        {
            if (result.Status != ParseResult.StatusCode.Found)
            {
                var content = new ParseMessageContent();
                content.Message = GetErrorMessage(result.Status);
                content.Log = result.ParserLog;
                content.ShowOptions = result.Status == ParseResult.StatusCode.ParseFailed || result.Status == ParseResult.StatusCode.InvalidOutputDir;

                CheckForSpecialResults(content);

                ParseMessageWindow.Display(content);
            }
        }

        public async System.Threading.Tasks.Task ParseAtCurrentLocationAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            OutputLog.Clear();

            LayoutWindow prewin = EditorUtils.GetLayoutWindow(false);
            if (prewin != null)
            {
                prewin.SetProcessing();
            }

            GeneralSettingsPageGrid settings = EditorUtils.GetGeneralSettings();
            parser.PrintCommandLine = settings.OptionParserShowCommandLine;
            parser.OutputDirectory = GetParserOutputDirectory();

            SolutionSettings solutionSettings = SettingsManager.Instance.Settings;

            //TODO ~ ramonv ~ add parsing queue to avoid multiple queries at the same time

            ParseResult result;
            DocumentLocation location = GetCurrentLocation();
            if (location == null)
            {
                OutputLog.Error(GetErrorMessage(ParseResult.StatusCode.InvalidLocation));
                result = new ParseResult { Status = ParseResult.StatusCode.InvalidLocation };
            }
            else if (solutionSettings.ExtractionTool == ParserTool.Clang)
            {
                EditorUtils.SaveActiveDocument();

                ProjectProperties properties = GetProjectData();
                if (properties == null)
                {
                    OutputLog.Error(GetErrorMessage(ParseResult.StatusCode.InvalidProject));
                    result = new ParseResult { Status = ParseResult.StatusCode.InvalidProject };
                }
                else
                {
                    result = await parser.ParseClangAsync(properties, location); 
                }
            }
            else if (solutionSettings.ExtractionTool == ParserTool.PDB)
            {
                string pdbPath = GetProjectExtractor().GetPDBPath();

                if (pdbPath == null || pdbPath.Length == 0)
                {
                    OutputLog.Error(GetErrorMessage(ParseResult.StatusCode.InvalidPDB));
                    result = new ParseResult { Status = ParseResult.StatusCode.InvalidPDB };
                }
                else if (!File.Exists(pdbPath))
                {
                    OutputLog.Error("Unable to find the PDB file at location " + pdbPath);
                    result = new ParseResult { Status = ParseResult.StatusCode.InvalidPDB };
                }
                else
                {
                    result = await parser.ParsePDBAsync(pdbPath, location);
                }
            }
            else
            {
                result = new ParseResult();
            }

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

            DisplayResult(result);
        }
    }
}

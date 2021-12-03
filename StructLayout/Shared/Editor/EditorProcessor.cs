using EnvDTE;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using System;

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

        private void DisplayResult(ParseResult result)
        {
            if (result.Status != ParseResult.StatusCode.Found)
            {
                var content = new ParseMessageContent();

                switch (result.Status)
                {
                    case ParseResult.StatusCode.InvalidOutputDir:
                        content.Message = "Unable to create the directory for the generated parser results.\nPlease make sure the parser output directory set in the Extension Options is valid and writable.";
                        break;
                    case ParseResult.StatusCode.VersionMismatch:
                        content.Message = "Parser result generated version does not match the current version."; 
                        break;
                    case ParseResult.StatusCode.InvalidInput:
                        content.Message = "Parser had Invalid Input.";
                        break;
                    case ParseResult.StatusCode.ParseFailed:
                        content.Message = "Errors found while parsing.\nUpdate the Extension's options as needed for a succesful compilation.\nCheck the 'Struct Layout' output pane for more information.";
                        break;
                    case ParseResult.StatusCode.NotFound:
                        content.Message = "No structure found at the given position.\nTry performing the query from a structure definition or initialization.";
                        break;
                }

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

            EditorUtils.SaveActiveDocument();

            DocumentLocation location = GetCurrentLocation();
            if (location == null)
            {
                string msg = "Unable to retrieve current document position.";
                OutputLog.Error(msg);
                ParseMessageWindow.Display(new ParseMessageContent(msg));
            }

            ProjectProperties properties = GetProjectData();
            if (properties == null)
            {
                string msg = "Unable to retrieve the project configuration";
                OutputLog.Error(msg);
                ParseMessageWindow.Display(new ParseMessageContent(msg));
                return;
            }

            GeneralSettingsPageGrid settings = EditorUtils.GetGeneralSettings();
            parser.PrintCommandLine = settings.OptionParserShowCommandLine;
            parser.OutputDirectory = GetParserOutputDirectory();

            //TODO ~ ramonv ~ add parsing queue to avoid multiple queries at the same time
           
            LayoutWindow prewin = EditorUtils.GetLayoutWindow(false);
            if (prewin != null) 
            { 
                prewin.SetProcessing();
            } 

            ParseResult result = await parser.ParseAsync(properties, location);

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

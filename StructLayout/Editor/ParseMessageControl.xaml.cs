using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StructLayout
{
    /// <summary>
    /// Interaction logic for ParseMessageControl.xaml
    /// </summary>
    public partial class ParseMessageControl : UserControl
    {
        private Window ParentWindow { set; get; }
        private ParseResult Result { set; get; }

        public ParseMessageControl(Window parentWindow,ParseResult result)
        {
            InitializeComponent();

            ParentWindow = parentWindow;
            Result = result;
            ShowMessage(); 
            ShowLog();
            ShowButtons(); 
        }

        private void ShowMessage()
        {
            mainContent.Children.Clear();

            switch(Result.Status)
            {
            case ParseResult.StatusCode.InvalidInput:
                mainContent.Children.Add(CreateBasicText("Parser had Invalid Input."));    
                break;
            case ParseResult.StatusCode.ParseFailed:
                mainContent.Children.Add(CreateBasicText("Errors found while parsing."));    
                mainContent.Children.Add(CreateBasicText("Update the Extension's options as needed for a succesful compilation."));    
                mainContent.Children.Add(CreateBasicText("Check the 'Struct Layout' output pane for more information."));    
                break;
            case ParseResult.StatusCode.NotFound:     
                mainContent.Children.Add(CreateBasicText("No structure found at the given position."));    
                mainContent.Children.Add(CreateBasicText("Try performing the query from a structure definition or initialization."));    
                break;
            }
        }

        private void ShowLog()
        {
            if (Result.ParserLog == null || Result.ParserLog.Length == 0)
            {
                logExpander.Visibility = Visibility.Collapsed;
                onlyButtons.Visibility = Visibility.Visible;
            }
            else
            {
                logExpander.Visibility = Visibility.Visible;
                onlyButtons.Visibility = Visibility.Collapsed;

                logText.Text = Result.ParserLog;
            }
        }

        private void ShowButtons()
        {
            if (Result.Status == ParseResult.StatusCode.ParseFailed)
            {
                buttonOptionsA.Visibility = Visibility.Visible; 
                buttonOptionsB.Visibility = Visibility.Visible;
                buttonAcceptB.IsDefault = false;
                buttonAcceptB.IsCancel = false;
                buttonAcceptA.IsDefault = true;
                buttonAcceptA.IsCancel = true;
            } 
            else
            {
                buttonOptionsA.Visibility = Visibility.Collapsed;
                buttonOptionsB.Visibility = Visibility.Collapsed;
                buttonAcceptA.IsDefault = false;
                buttonAcceptA.IsCancel = false;
                buttonAcceptB.IsDefault = true;
                buttonAcceptB.IsCancel = true;
            }
        }

        private TextBlock CreateBasicText(string str)
        {
            var ret = new TextBlock();
            ret.Text = str;
            return ret; 
        }

        private void CloseWindow(object a = null, object b = null)
        { 
            ParentWindow.Close();
        }

        private void OpenSettings(object a = null, object b = null)
        {
            ParentWindow.Close();
            SettingsManager.Instance.OpenSettingsWindow();
        }

        private void buttonOptionsB_KeyDown(object sender, KeyEventArgs e)
        {

        }
    }
}

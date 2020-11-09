using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace StructLayout
{
    public class ParseMessageWindow : Window
    {
        public ParseMessageWindow(ParseResult result)
        {
            Title = "Struct Layout";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            this.Content = new ParseMessageControl(this,result);
        }

        static public void DisplayResult(ParseResult result)
        {
            if (result.Status != ParseResult.StatusCode.Found)
            {
                var messageWin = new ParseMessageWindow(result);
                messageWin.ShowDialog();
            }
        }
    }
}

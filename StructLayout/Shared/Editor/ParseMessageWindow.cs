using System.Windows;

namespace StructLayout
{
    public class ParseMessageWindow : Window
    {
        public ParseMessageWindow(ParseMessageContent content)
        {
            Title = "Struct Layout";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            this.Content = new ParseMessageControl(this, content);
        }

        static public void Display(ParseMessageContent content)
        {
            var messageWin = new ParseMessageWindow(content);
            messageWin.ShowDialog();
        }
    }
}

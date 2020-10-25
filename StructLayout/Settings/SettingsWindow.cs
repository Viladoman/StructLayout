using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace StructLayout.Settings
{
    class SettingsWindow : Window
    {
        public SettingsWindow(SolutionSettings settings)
        {
            Title = "Struct Layout Options";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SizeToContent = SizeToContent.Height;
            Width =  900;

            this.Content = new SettingsControl(settings == null? new SolutionSettings() : settings);
        }

    }
}

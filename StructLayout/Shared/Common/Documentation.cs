using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StructLayout
{
    static public class Documentation
    {
        public enum Link
        {
            None,
            MainPage,
            ReportIssue,
            GeneralConfiguration,
            Donate,
        }

        static public string LinkToURL(Link link)
        {
            switch (link)
            {
                case Link.MainPage:             return @"https://github.com/Viladoman/StructLayout";
                case Link.ReportIssue:          return @"https://github.com/Viladoman/StructLayout/issues";
                case Link.GeneralConfiguration: return @"https://github.com/Viladoman/StructLayout/wiki/Configurations";
                case Link.Donate:               return @"https://www.paypal.com/donate?hosted_button_id=QWTUS8PNK5X5A";
            }
            return null;
        }

        static public void OpenLink(Link link)
        {
            string urlStr = LinkToURL(link);
            if (urlStr != null)
            {  
                var uri = new Uri(urlStr);
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri));
            }
        }
    }
}

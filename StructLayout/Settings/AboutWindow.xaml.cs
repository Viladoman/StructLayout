using System;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Xml;

namespace Company.Product
{
    public class VsixManifest
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public VsixManifest(string manifestPath)
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);

            if (doc.DocumentElement == null || doc.DocumentElement.Name != "PackageManifest") return;

            var metaData = doc.DocumentElement.ChildNodes.Cast<XmlElement>().First(x => x.Name == "Metadata");
            var identity = metaData.ChildNodes.Cast<XmlElement>().First(x => x.Name == "Identity");

            Id = identity.GetAttribute("Id");
            Version = identity.GetAttribute("Version");
        }

        public static VsixManifest GetManifest()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyUri = new UriBuilder(assembly.CodeBase);
            var assemblyPath = Uri.UnescapeDataString(assemblyUri.Path);
            var assemblyDirectory = System.IO.Path.GetDirectoryName(assemblyPath);
            var vsixManifestPath = System.IO.Path.Combine(assemblyDirectory, "extension.vsixmanifest");

            return new VsixManifest(vsixManifestPath);
        }
    }
}

namespace StructLayout
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();

            var manifest = Company.Product.VsixManifest.GetManifest();

            extVersionTxt.Text = "Version: " + manifest.Version;
            clangVersionTxt.Text = "Clang Version: 11.0.0";
        }

        private void OnReportIssue(object sender, object e)
        {
            this.Close();
            Documentation.OpenLink(Documentation.Link.ReportIssue);
        }

        private void OnGithub(object sender, object e)
        {
            this.Close();
            Documentation.OpenLink(Documentation.Link.MainPage);
        }

        private void OnDonate(object sender, object e)
        {
            this.Close();
            Documentation.OpenLink(Documentation.Link.Donate);
        }

        private void OnClose(object sender, object e)
        {
            this.Close();
        }
    }
}

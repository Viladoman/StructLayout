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
    /// Interaction logic for LayoutNodeTooltip.xaml
    /// </summary>
    public partial class LayoutNodeTooltip : UserControl
    {
        private LayoutNode node = null;
        public LayoutNode ReferenceNode
        {
            set { node = value; OnNode(); }
            get { return node; }
        }

        public LayoutNodeTooltip()
        {
            InitializeComponent();
        }

        private void OnNode()
        {
            if (node != null)
            {
                RefreshBasicProfile();
                //RefreshTypeStack();
                RefreshInteractionText();
            }
        }

        private void RefreshBasicProfile()
        {
            if (node.Name.Length > 0)
            {
                headerTxt.Text = node.Name;

                subheaderTxt.Visibility = Visibility.Visible;
                subheaderTxt.Text = node.Type;
            }
            else if (node.Type.Length > 0)
            {
                headerTxt.Text = node.Type;
                subheaderTxt.Visibility = Visibility.Visible;
                subheaderTxt.Text = node.Category.ToString();
            }
            else
            {
                headerTxt.Text = node.Category.ToString();
                subheaderTxt.Visibility = Visibility.Collapsed;
            }

            //LayoutData

            var localOffset = node.Parent == null ? node.Offset : node.Offset - node.Parent.Offset;
            if (localOffset == node.Offset)
            {
                offsetTxt.Text = "Offset: "+node.Offset;
            }
            else
            {
                offsetTxt.Text = "Offset: "+node.Offset+" (Local: "+localOffset+")";
            }

            sizeTxt.Text = "Size: " + node.Size;
            //alignTxt.Text = "Align: " + node.Align;
            alignTxt.Text = "InnerTop: "+ node.RenderData.Paddings[(int)RenderData.PaddingSide.InnerTop]; //TODO ~ Ramonv ~ remove 
        }
        /*
        private void RefreshTypeStack()
        {
            typeStack.Children.Clear();

            if (node.Parent == null)
            {
                typeBorder.Visibility = Visibility.Collapsed;
                typeStack.Visibility = Visibility.Collapsed;
            }
            else
            {
                //TODO ~ ramonv ~ to be implemented
            }
        }
        */
        private void RefreshInteractionText()
        {
            if (node.Children.Count == 0)
            {
                interactionBorder.Visibility = Visibility.Collapsed;
                interactionPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                interactionBorder.Visibility = Visibility.Visible;
                interactionPanel.Visibility = Visibility.Visible;
                interactionTxt.Text = "Left Mouse Click to Expand/Collapse";
            }
        }
    }
}

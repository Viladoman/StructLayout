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
        private LayoutNode Node { set; get; }
        public LayoutNode ReferenceNode
        {
            set { Node = value; OnNode(); }
            get { return Node; }
        }

        public LayoutNodeTooltip()
        {
            InitializeComponent();
        }

        private void OnNode()
        {
            if (Node != null)
            {
                RefreshBasicProfile();
                RefreshUnionStack();
                RefreshTypeStack();
                RefreshInteractionText();
            }
        }

        private void RefreshBasicProfile()
        {
            if (Node.Name.Length > 0)
            {
                headerTxt.Text = Node.Name;

                subheaderTxt.Visibility = Visibility.Visible;
                subheaderTxt.Text = Node.Type;
            }
            else if (Node.Type.Length > 0)
            {
                headerTxt.Text = Node.Type;
                subheaderTxt.Visibility = Visibility.Visible;
                subheaderTxt.Text = Node.Category.ToString();
            }
            else
            {
                headerTxt.Text = Node.Category.ToString();
                subheaderTxt.Visibility = Visibility.Collapsed;
            }

            //LayoutData

            //TODO ~ ramonv ~ add padding - % padding - realsize

            layout1Txt.Text = "Offset: " + GetFullValueStr(Node.Offset) + " - Size: "  + GetFullValueStr(Node.Size) + " - Align: " + GetFullValueStr(Node.Align);

            var localOffset = Node.Parent == null ? Node.Offset : Node.Offset - Node.Parent.Offset;
            layout2Txt.Visibility = localOffset == Node.Offset ? Visibility.Collapsed : Visibility.Visible;
            layout2Txt.Text = "Local Offset: " + GetFullValueStr(localOffset);
        }

        private void RefreshUnionStack()
        {
            extraStack.Children.Clear();

            if (Node.Extra.Count > 0)
            {
                extraBorder.Visibility = Visibility.Visible;
                extraStack.Visibility = Visibility.Visible;
                var title = new TextBlock();
                title.Text = Node.Category == LayoutNode.LayoutCategory.Union? "Contains:" : "Empty Base Optimization:";
                extraStack.Children.Add(title);

                foreach(LayoutNode child in Node.Extra)
                {
                    var desc = new TextBlock();
                    desc.Text = "- " + (child.Type.Length > 0 ? child.Type : child.Category.ToString()) + (child.Name.Length > 0? " " + child.Name : "") + " ( size: " + GetFullValueStr(child.Size) + " )";
                    extraStack.Children.Add(desc);
                }
            }
            else
            {
                extraBorder.Visibility = Visibility.Collapsed;
                extraStack.Visibility = Visibility.Collapsed;
            }
        }

        private void BuildTypeStack(LayoutNode node)
        {
            var entry = new TextBlock();
            entry.Text = "- " + (node.Type.Length > 0? node.Type : node.Category.ToString());
            typeStack.Children.Add(entry);
            if (node.Parent != null)
            {
                BuildTypeStack(node.Parent);
            }
        }

        private void RefreshTypeStack()
        {
            typeStack.Children.Clear();

            if (Node.Parent == null)
            {
                typeBorder.Visibility = Visibility.Collapsed;
                typeStack.Visibility = Visibility.Collapsed;
            }
            else
            {
                typeBorder.Visibility = Visibility.Visible;
                typeStack.Visibility = Visibility.Visible;
                var title = new TextBlock();
                title.Text = "Parent Stack";
                typeStack.Children.Add(title);
                BuildTypeStack(Node.Parent);
            }
        }

        private void RefreshInteractionText()
        {
            if (Node.Children.Count == 0 || Node.Category == LayoutNode.LayoutCategory.Union)
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
        public static string GetFullValueStr(uint value)
        {
            return value < 10? value.ToString() : value + " ( 0x" + value.ToString("X") + " )";
        }
    }
}

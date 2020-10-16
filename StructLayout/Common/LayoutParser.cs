using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace StructLayout
{
    public class DocumentLocation
    {
        public DocumentLocation(string filename, uint line, uint column)
        {
            Filename = filename;
            Line = line;
            Column = column;
        }

        public string Filename { get; }
        public uint Line { get; }
        public uint Column { get; }
    };

    public class ProjectProperties
    {
        public enum TargetType
        {
            x86,
            x64,
        }

        public List<string> IncludeDirectories { set; get; }
        public List<string> PrepocessorDefinitions { set; get; }
        public TargetType Target { set; get; }
    }

    public class RenderData
    {
        public enum ShapeCategory
        {
            Invalid,
            Simple, 
            Split, 
            Blob
        }

        public enum PaddingSide
        {
            OuterTop = 0,
            InnerTop,
            OuterLeft,
            InnerLeft,
            OuterBottom,
            InnerBottom,
            OuterRight,
            InnerRight,

            Count
        }

        public RenderData()
        {
            Category = ShapeCategory.Invalid;
            Paddings = new uint[Enum.GetNames(typeof(PaddingSide)).Length];
        }

        public uint[] Paddings { set; get; }
        public Brush Background { set; get; }
        public ShapeCategory Category { set; get; }
        public Point[] Points { set; get; }
    }

    public class LayoutNode
    {
        public enum LayoutCategory
        {
            Root = 0,
            SimpleField,
            Bitfield,
            ComplexField,
            VPrimaryBase,
            VBase,
            NVPrimaryBase,
            NVBase,
            VTablePtr,
            VFTablePtr,
            VBTablePtr,
            VtorDisp,
            Padding,
        };

        public LayoutNode()
        {
            Children = new List<LayoutNode>();
            RenderData = new RenderData();
            Collapsed = true;
        }

        public string Type { set; get; }
        public string Name { set; get; }

        public uint Offset { set; get; }
        public uint Size { set; get; }
        public uint Align { set; get; }

        public bool Collapsed { set; get; }

        public LayoutCategory Category { set; get; }

        public LayoutNode Parent { set; get; }
        public List<LayoutNode> Children { get; }

        //Render params
        public RenderData RenderData { set; get; }

        public void AddChild(LayoutNode childNode)
        {
            Children.Add(childNode);
            childNode.Parent = this;
        }
    }

    public class LayoutParser
    {
        [DllImport("LayoutParser.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool ParseLocation(string commandline, string fullFilename, uint row, uint col);

        [DllImport("LayoutParser.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetData(ref uint size);

        [DllImport("LayoutParser.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Clear();

        private LayoutNode ReadNode(BinaryReader reader)
        {
            LayoutNode node = new LayoutNode();
            node.Type = reader.ReadString();
            node.Name = reader.ReadString();

            //TODO ~ ramonv ~ have a thought on what to do when numbers bigger than uint arrive
            node.Offset = (uint)reader.ReadInt64();
            node.Size = (uint)reader.ReadInt64();
            node.Align = (uint)reader.ReadInt64();
            node.Category = (LayoutNode.LayoutCategory)reader.ReadByte();

            uint numChildren = reader.ReadUInt32();
            for (uint i = 0; i < numChildren; ++i)
            {
                node.AddChild(ReadNode(reader));
            }

            return node;
        }

        public void FinalizeNodeRecursive(LayoutNode node)
        {
            node.Offset += node.Parent != null ? node.Parent.Offset : 0;
            node.RenderData.Background = Colors.GetCategoryBackground(node.Category);

            foreach (LayoutNode child in node.Children)
            {
                FinalizeNodeRecursive(child);
            }
        }

        public void FinalizeNode(LayoutNode node)
        {
            node.Collapsed = false;
            FinalizeNodeRecursive(node);
        }


        public LayoutNode Parse(ProjectProperties projProperties, DocumentLocation location)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (location.Filename == null || location.Filename.Length == 0)
            {
                OutputLog.Error("No file provided for parsing");
                return null;
            }

            //llvm outs is not going through console
            //var sw = new StringWriter();
            //Console.SetOut(sw);
            //Console.SetError(sw);
            //Console.WriteLine("Hello world.");

            //TODO ~ ramonv ~ pass in context

            LayoutNode ret = null;

            string includes  = GenerateCommandStr("-I",projProperties.IncludeDirectories);
            string defines   = GenerateCommandStr("-D",projProperties.PrepocessorDefinitions);
            
            string archStr = projProperties != null && projProperties.Target == ProjectProperties.TargetType.x86 ? "-m32" : "-m64";
            string toolCmd = "--show " + location.Filename + " -- clang++ -x c++ " + archStr + defines + includes;

            if (ParseLocation(toolCmd, location.Filename, location.Line, location.Column))
            {
                //capture data
                uint size = 0;
                IntPtr result = GetData(ref size);
                byte[] managedArray = new byte[size];
                Marshal.Copy(result, managedArray, 0, (int)size);

                using (BinaryReader reader = new BinaryReader(new MemoryStream(managedArray)))
                {
                    ret = ReadNode(reader);
                    FinalizeNode(ret);
                }

                Clear();
            }
            else
            {
                OutputLog.Log("Unable to find the struct.");
            }

            return ret;

        }

        private string GenerateCommandStr(string prefix, List<string> args)
        {
            string ret = "";
            if (args != null)
            {
                foreach (string value in args)
                {
                    ret += " "+ prefix + value;
                }
            }

            return ret; 
        }
    }
}

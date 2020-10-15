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
    public class RenderData
    {
        public enum ShapeCategory
        {
            Invalid,
            Simple, 
            Split, 
            Blob
        }

        public enum PaddingFlag
        {
            OuterTop    = 1 << 0,
            InnerTop    = 1 << 1,
            OuterLeft   = 1 << 2,
            InnerLeft   = 1 << 3,
            OuterBottom = 1 << 4,
            InnerBottom = 1 << 5,       
            OuterRight  = 1 << 6,
            InnerRight  = 1 << 7,       

            All         = OuterTop | InnerTop | OuterLeft | InnerLeft | OuterBottom | InnerBottom | OuterRight | InnerRight
        }

        public RenderData(uint depth = 0u)
        {
            Depth = depth;
            Category = ShapeCategory.Invalid;
            PaddingFlags = 0u;
        }

        public PaddingFlag PaddingFlags { set; get; }
        public uint Depth { set; get; }

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
        public static extern bool ParseType(string commandline, string typeName);

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

        public LayoutNode Parse(ProjectProperties projProperties, EditorPosition position)
        {
            //llvm outs is not going through console
            //var sw = new StringWriter();
            //Console.SetOut(sw);
            //Console.SetError(sw);
            //Console.WriteLine("Hello world.");

            //TODO ~ ramonv ~ pass in context

            LayoutNode ret = null;


            /*

            $LLVM_BUILD/bin/libtool-example clang/tools/clang-check/ClangCheck.cpp --   \
                                 clang++ -D__STDC_CONSTANT_MACROS -D__STDC_LIMIT_MACROS \
                                 -Itools/clang/include -I$LLVM_BUILD/include -Iinclude  \
                                 -Itools/clang/lib/Headers -c
         */

            // -IIncludePath -DMACRO -DMACRO=value -UMacro (undefine macro)

            string archStr = projProperties != null && projProperties.Target == ProjectProperties.TargetType.x86 ? "-m32" : "-m64";
            string toolCmd = "--show " + position.Filename + " -- clang++ -x c++ " + archStr;

            if (ParseLocation(toolCmd, position.Filename, position.Line + 1, position.Column + 1))
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

            return ret;

        }

        //TODO ~ ramonv ~ PLACEHOLDER

        static private LayoutNode CreateFakeNode(uint offset, uint size)
        {
            LayoutNode node = new LayoutNode();
            node.Size = size;
            node.Align = 4;
            node.Offset = offset;
            node.Type = "FakeType";
            node.Name = "Fake";
            return node;
        }

        static public LayoutNode ParseFake()
        {
            LayoutNode root = new LayoutNode();
            root.Size = 35;
            root.Align = 8;
            root.Offset = 0;
            root.Type = "CustomType";
            root.Name = "FakeRoot";

            var smallChild = CreateFakeNode(13, 1);
            var bigChild = CreateFakeNode(14, 21);

            root.AddChild(CreateFakeNode(0, 1));
            root.AddChild(CreateFakeNode(1, 1));
            root.AddChild(CreateFakeNode(2, 2));
            root.AddChild(CreateFakeNode(4, 5));
            root.AddChild(CreateFakeNode(9, 4));
            root.AddChild(smallChild);
            root.AddChild(bigChild);

            smallChild.AddChild(CreateFakeNode(0, 1));

            var superChild = CreateFakeNode(6, 14);
            bigChild.AddChild(CreateFakeNode(0, 1));
            bigChild.AddChild(CreateFakeNode(1, 5));
            bigChild.AddChild(superChild);

            superChild.AddChild(CreateFakeNode(0, 4));
            superChild.AddChild(CreateFakeNode(4, 4));
            superChild.AddChild(CreateFakeNode(8, 4));
            superChild.AddChild(CreateFakeNode(12, 2));

            var fakeParser = new LayoutParser(); 
            fakeParser.FinalizeNode(root);
            return root;
        }
    }
}

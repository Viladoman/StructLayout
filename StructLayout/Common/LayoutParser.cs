using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
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

        public List<string> IncludeDirectories { set; get; } = new List<string>();
        public List<string> ForceIncludes { set; get; } = new List<string>();
        public List<string> PrepocessorDefinitions { set; get; } = new List<string>();
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

        public uint[] Paddings { set; get; } = new uint[Enum.GetNames(typeof(PaddingSide)).Length];
        public Brush Background { set; get; }
        public ShapeCategory Category { set; get; } = ShapeCategory.Invalid;
        public Point[] Points { set; get; }
        public Point TextPosition { set; get; }
        public FormattedText Text { set; get; }
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
            Union,
            Padding,
            
        };

        public string Type { set; get; } = "";
        public string Name { set; get; } = "";

        public uint Offset { set; get; }
        public uint Size { set; get; }
        public uint Align { set; get; }

        public bool Collapsed { set; get; } = true;

        public LayoutCategory Category { set; get; }

        public LayoutNode Parent { set; get; }
        public List<LayoutNode> Children { set; get; } = new List<LayoutNode>();
        public List<LayoutNode> Extra { set; get; } = new List<LayoutNode>();

        //Render params
        public RenderData RenderData { set; get; } = new RenderData();

        public void AddChild(LayoutNode childNode)
        {
            Children.Add(childNode);
            childNode.Parent = this;
        }

        public bool IsBaseCategory()
        {
            switch (Category)
            {
                case LayoutNode.LayoutCategory.NVPrimaryBase:
                case LayoutNode.LayoutCategory.NVBase:
                case LayoutNode.LayoutCategory.VBase:
                case LayoutNode.LayoutCategory.VPrimaryBase:
                    return true;
                default:
                    return false;
            }
        }
    }

    public class ParseResult
    {
        public enum StatusCode
        {
            InvalidInput,
            ParseFailed,
            NotFound,
            Found
        }

        public LayoutNode Layout { set; get; }
        public StatusCode Status { set; get; }
    }

    public class LayoutParser
    {
        public string ExtraArgs { get; set; } = "";
        public bool PrintCommandLine { get; set; } = false;
        public bool ShowWarnings { get; set; } = false;

        [DllImport("LayoutParser.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool ParseLocation(string commandline, string fullFilename, uint row, uint col);

        [DllImport("LayoutParser.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetData(ref uint size);

        [DllImport("LayoutParser.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr GetLog(ref uint size);

        [DllImport("LayoutParser.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern void Clear();

        private LayoutNode ReadNode(BinaryReader reader)
        {
            LayoutNode node = new LayoutNode();
            node.Type = reader.ReadString();
            node.Name = reader.ReadString();

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

        private void FixOverlaps(LayoutNode node)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (node.Type.Length > 0 && node.Type.StartsWith("union"))
            {
                node.Category = LayoutNode.LayoutCategory.Union;
                node.Extra    = node.Children;
                node.Children = new List<LayoutNode>();
            }
            
            if (node.Category != LayoutNode.LayoutCategory.Union)
            {
                for (int i = 1; i < node.Children.Count;)
                {
                    var thisNode = node.Children[i];
                    var prevNode = node.Children[i - 1];

                    if (thisNode.Offset == prevNode.Offset)
                    {
                        if (prevNode.IsBaseCategory() && prevNode.Size == 1)
                        {
                            //Empty base optimization
                            node.Extra.Add(prevNode);
                            node.Children.RemoveAt(i - 1);
                        }
                        else
                        {
                            //Found unknown overlap 
                            OutputLog.Log("Found type overlap without known explanation");
                            ++i;
                        }
                    }
                    else
                    {
                        ++i;
                    }
                }
            }
            
            //continue recursion
            foreach (LayoutNode child in node.Children)
            {
                FixOverlaps(child);
            }
        }

        public void FinalizeNode(LayoutNode node)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            FixOverlaps(node);

            node.Collapsed = false;
            FinalizeNodeRecursive(node);
        }

        public void ProcessLog()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            uint size = 0;
            IntPtr result = GetLog(ref size);
            if (size > 0)
            {
                byte[] managedArray = new byte[size];
                Marshal.Copy(result, managedArray, 0, (int)size);
                string val = Encoding.UTF8.GetString(managedArray);
                OutputLog.Log("Execution Log:\n"+val);
            }
        }

        public async Task<ParseResult> ParseAsync(ProjectProperties projProperties, DocumentLocation location)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            ParseResult ret = new ParseResult(); 

            //TODO ~ ramonv ~ shortcut this or queue if we are already processing something

            if (location.Filename == null || location.Filename.Length == 0)
            {
                OutputLog.Error("No file provided for parsing");
                ret.Status = ParseResult.StatusCode.InvalidInput;
                return ret;
            }

            string includes  = GenerateCommandStr("-I",projProperties.IncludeDirectories);
            string forceInc  = GenerateCommandStr("-include", projProperties.ForceIncludes);
            string defines   = GenerateCommandStr("-D",projProperties.PrepocessorDefinitions);
            string flags     = ShowWarnings? "" : " -w";
            string extra     = ExtraArgs.Length == 0? "" : " " + ExtraArgs;

            string archStr = projProperties != null && projProperties.Target == ProjectProperties.TargetType.x86 ? "-m32" : "-m64";
            string toolCmd = location.Filename + " -- -x c++ " + archStr + flags + defines + includes + forceInc + extra;

            OutputLog.Focus();
            OutputLog.Log("Looking for structures at " + location.Filename + ":" + location.Line + ":" + location.Column+"...");

            if (PrintCommandLine)
            {
                OutputLog.Log("COMMAND LINE: " + toolCmd);
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();

            var valid = await System.Threading.Tasks.Task.Run(() => ParseLocation(toolCmd, location.Filename, location.Line, location.Column));

            watch.Stop();
            const long TicksPerMicrosecond = (TimeSpan.TicksPerMillisecond / 1000);
            string timeStr = " ("+GetTimeStr((ulong)(watch.ElapsedTicks / TicksPerMicrosecond))+")";

            ProcessLog();

            if (valid)
            {
                //capture data
                uint size = 0;
                IntPtr result = GetData(ref size);

                if (size > 0)
                {
                    byte[] managedArray = new byte[size];
                    Marshal.Copy(result, managedArray, 0, (int)size);

                    using (BinaryReader reader = new BinaryReader(new MemoryStream(managedArray)))
                    {
                        ret.Layout = ReadNode(reader);
                        FinalizeNode(ret.Layout);
                    }

                    OutputLog.Log("Found structure " + ret.Layout.Type + "." + timeStr);
                    ret.Status = ParseResult.StatusCode.Found;
                }
                else
                {
                    OutputLog.Log("No structure found at the given location." + timeStr);
                    ret.Status = ParseResult.StatusCode.NotFound;
                }
            }
            else
            {
                OutputLog.Error("Unable to scan the given location." + timeStr);
                ret.Status = ParseResult.StatusCode.ParseFailed;
            }

            Clear();

            return ret;
        }

        private string GenerateCommandStr(string prefix, List<string> args)
        {
            string ret = "";
            if (args != null)
            {
                foreach (string value in args)
                {
                    ret += " " + prefix + value;
                }
            }

            return ret; 
        }

        static public string GetTimeStr(ulong uSeconds)
        {
            ulong ms = uSeconds / 1000;
            ulong us = uSeconds - (ms * 1000);
            ulong sec = ms / 1000;
            ms = ms - (sec * 1000);
            ulong min = sec / 60;
            sec = sec - (min * 60);
            ulong hour = min / 60;
            min = min - (hour * 60);

            if (hour > 0) { return hour + " h " + min + " m"; }
            if (min > 0) { return min + " m " + sec + " s"; }
            if (sec > 0) { return sec + "." + ms.ToString().PadLeft(4, '0') + " s"; }
            if (ms > 0) { return ms + "." + us.ToString().PadLeft(4, '0') + " ms"; }
            if (us > 0) { return us + " μs"; }
            return "< 1 μs";
        }
    }
}

using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

        public enum StandardVersion
        {
            Default, 
            Cpp98,
            Cpp03,
            Cpp14,
            Cpp17,
            Cpp20,
            Gnu98,
            Gnu03,
            Gnu14,
            Gnu17,
            Gnu20,
            Latest,
        }

        public List<string> IncludeDirectories { set; get; } = new List<string>();
        public List<string> ForceIncludes { set; get; } = new List<string>();
        public List<string> PrepocessorDefinitions { set; get; } = new List<string>();
        public string WorkingDirectory { set; get; } = "";
        public string ExtraArguments { set; get; } = "";
        public bool ShowWarnings { set; get; } = false;

        public TargetType Target { set; get; } = TargetType.x64;
        public StandardVersion Standard { set; get; } = StandardVersion.Default;
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

    public class LayoutLocation
    {
        public string Filename { set; get; }
        public uint   Line { set; get; }
        public uint   Column { set; get; }
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
            Shared,

        };

        public string Type { set; get; } = "";
        public string Name { set; get; } = "";

        public uint Offset { set; get; }
        public uint Size { set; get; }
        public uint Align { set; get; }
        public uint RealSize { set; get; }
        public uint Padding { get { return Size - RealSize; } }

        public LayoutCategory Category { set; get; }
        public LayoutLocation TypeLocation { set; get; }
        public LayoutLocation FieldLocation { set; get; }

        public LayoutNode Parent { set; get; }
        public List<LayoutNode> Children { set; get; } = new List<LayoutNode>();
        public List<LayoutNode> Extra { set; get; } = new List<LayoutNode>();

        //Render params
        public RenderData RenderData { set; get; } = new RenderData();

        public bool IsExpanded { set; get; } = false;
        public int? ExpansionIndex { set; get; } = null;

        public bool Expand(int? index = null)
        {
            bool isShared = IsSharedMemory();

            if (Children.Count > 0 && (!isShared || Children.Count == 1 || (index.HasValue && index.Value < Children.Count)))
            {
                IsExpanded = true;
                ExpansionIndex = index;
                return true;
            }
            return false;
        }

        public void Collapse()
        {
            IsExpanded = false;
            ExpansionIndex = null;
        }

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

        public bool IsSharedMemory()
        {
            return Category == LayoutNode.LayoutCategory.Union || Category == LayoutNode.LayoutCategory.Shared;
        }
    }

    public class ParseResult
    {
        public enum StatusCode
        {
            InvalidOutputDir,
            VersionMismatch,
            InvalidInput,
            ParseFailed,
            NotFound,
            Found
        }

        public LayoutNode Layout { set; get; }
        public StatusCode Status { set; get; }
        public string ParserLog { set; get; }
    }

    public class LayoutParser
    {
        public bool PrintCommandLine { get; set; } = false;
        public string OutputDirectory { get; set; } = null;

        public const uint VERSION = 1;
      
        private string GetToolPath(string localPath)
        {
            string installDirectory = EditorUtils.GetExtensionInstallationDirectory();
            string ret = installDirectory == null ? null : installDirectory + localPath;
            return File.Exists(ret) ? ret : null;
        }

        private string GetClangLayoutParserToolPath()
        {
            return GetToolPath(@"External\ClangLayout.exe");
        }

        private LayoutLocation ReadLocation(BinaryReader reader, List<string> files)
        {
            int fileIndex = reader.ReadInt32();

            if (fileIndex < 0 || fileIndex >= files.Count)
            {
                return null;
            }

            LayoutLocation ret = new LayoutLocation { Filename = files[fileIndex] };

            ret.Line   = reader.ReadUInt32(); 
            ret.Column = reader.ReadUInt32();

            return ret;
        }

        private LayoutNode ReadNode(BinaryReader reader, List<string> files)
        {
            LayoutNode node = new LayoutNode();
            node.Type = reader.ReadString();
            node.Name = reader.ReadString();

            node.Offset = (uint)reader.ReadInt64();
            node.Size = (uint)reader.ReadInt64();
            node.Align = (uint)reader.ReadInt64();
            node.Category = (LayoutNode.LayoutCategory)reader.ReadByte();

            node.TypeLocation = ReadLocation(reader, files);
            node.FieldLocation = ReadLocation(reader, files);

            uint numChildren = reader.ReadUInt32();
            for (uint i = 0; i < numChildren; ++i)
            {
                node.AddChild(ReadNode(reader, files));
            }

            return node;
        }

        private List<string> ReadFiles(BinaryReader reader)
        {
            uint numFiles = reader.ReadUInt32();
            List<string> output = new List<string>((int)numFiles);

            for (uint i = 0; i < numFiles; ++i)
            {
                output.Add(reader.ReadString());
            }

            return output;
        }

        private void FinalizeNodeRecursive(LayoutNode node)
        {
            node.Offset += node.Parent != null ? node.Parent.Offset : 0;
            node.RenderData.Background = Colors.GetCategoryBackground(node.Category);

            node.RealSize = node.Children.Count == 0? node.Size : 0;
            uint realSizeOffset = node.Offset;
            foreach (LayoutNode child in node.Children)
            {
                FinalizeNodeRecursive(child);

                node.RealSize += realSizeOffset <= child.Offset ? child.RealSize : 0;
                realSizeOffset = Math.Max(child.Offset+child.Size,realSizeOffset);
            }

            //For shared memory nodes, children will be overlaped 
            if (node.Category == LayoutNode.LayoutCategory.Shared || node.Category == LayoutNode.LayoutCategory.Union)
            {
                foreach (LayoutNode child in node.Children)
                {
                    node.RealSize = Math.Max(child.RealSize, node.RealSize);
                }
            }
        }

        private void FixUnions(LayoutNode node)
        {
            if (node.Type.Length > 0 && node.Type.StartsWith("union"))
            {
                node.Category = LayoutNode.LayoutCategory.Union;
            }
        }

        private void AdjustBitfieldNode(LayoutNode node)
        {
            node.Extra = node.Children;
            node.Children = new List<LayoutNode>();

            foreach (LayoutNode child in node.Extra)
            {
                child.Name     = node.Name;
                child.Type     = node.Type;
                child.Category = node.Category;
            }
        }

        private void MergeBitfieldNodes(LayoutNode source, LayoutNode target)
        {
            foreach (LayoutNode child in source.Extra)
            {
                target.Extra.Add(child);
            }

            if (target.Extra.Count > 1)
            {
                target.Name = "Bitfield";
            }
        }

        private void FixBitfields(LayoutNode node)
        {
            LayoutNode prevNode = null;
            for (int i = 0; i < node.Children.Count;)
            {
                LayoutNode thisNode = node.Children[i];

                if (thisNode.Category == LayoutNode.LayoutCategory.Bitfield)
                {
                    AdjustBitfieldNode(thisNode);

                    if (prevNode != null && prevNode.Category == LayoutNode.LayoutCategory.Bitfield && (thisNode.Offset == prevNode.Offset || thisNode.Offset < prevNode.Offset + prevNode.Size))
                    {
                        MergeBitfieldNodes(thisNode, prevNode);
                        node.Children.RemoveAt(i);
                        continue;
                    }
                }

                prevNode = thisNode;
                ++i;
            }           
        }

        private void ShareNodes(LayoutNode nodeA, LayoutNode nodeB)
        {
            LayoutNode parent = nodeA.Parent;

            LayoutNode share;

            //Create or update the Shared node
            if (nodeA.Category != LayoutNode.LayoutCategory.Shared)
            {
                //Create the new node
                share = new LayoutNode();
                share.Name = "Shared Memory";
                share.Category = LayoutNode.LayoutCategory.Shared;
                share.Offset = nodeA.Offset;
                share.Size = nodeA.Size;
                nodeA.Offset = 0;

                //inject the new node
                int index = parent.Children.IndexOf(nodeA);
                share.Children.Add(nodeA);
                parent.Children[index] = share;

                share.Parent = parent;
                nodeA.Parent = share;
            }
            else
            {
                share = nodeA;
            }

            //perform the merge
            share.Size = Math.Max(share.Size, nodeB.Size);
            nodeB.Offset = 0;
            share.Children.Add(nodeB);
            nodeB.Parent = share;
            parent.Children.Remove(nodeB);
        }

        private void FixEmptyTypes(LayoutNode node)
        {
            for (int i = 0; i < node.Children.Count;)
            {
                var childNode = node.Children[i];
                if (childNode.Size == 0)
                {
                    //We don't accept elements of 0 size ( can be Empty Base Optimization or something unexpected )
                    node.Children.RemoveAt(i);
                    node.Extra.Add(childNode);
                }
                else
                {
                    ++i;
                }
            }
        }

        private void FixSharedMemory(LayoutNode node)
        {
            for (int i = 1; i < node.Children.Count;)
            {
                var thisNode = node.Children[i];
                var prevNode = node.Children[i - 1];

                if (thisNode.Offset == prevNode.Offset)
                {
                    ShareNodes(prevNode,thisNode);
                }
                else
                {
                    ++i;
                }
            }
        }

        private void FixOverlaps(LayoutNode node)
        {
            FixUnions(node);

            if (!node.IsSharedMemory())
            {
                FixEmptyTypes(node);
                FixBitfields(node);
                FixSharedMemory(node);
            }

            //continue recursion
            foreach (LayoutNode child in node.Children)
            {
                FixOverlaps(child);
            }
        }

        public void FinalizeNode(LayoutNode node)
        {
            FixOverlaps(node);

            node.Expand();

            FinalizeNodeRecursive(node);
        }

        private string CreateDirectory(string path)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                Directory.CreateDirectory(path);
            }
            catch (Exception e)
            {
                string errorStr = "Unable to create directory '" + path + "'.\n" + e.ToString();
                OutputLog.Error(errorStr);
                return errorStr;
            }

            return null;
        }

        public ParseResult LoadParseResult( string fullPath )
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            ParseResult ret = new ParseResult();

            if (!File.Exists(fullPath))
            {
                OutputLog.Log("No structure found at the given location.");
                ret.Status = ParseResult.StatusCode.NotFound;
                return ret;
            }
            
            FileStream fileStream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using (BinaryReader reader = new BinaryReader(fileStream))
            {
                // Read version
                uint thisVersion = reader.ReadUInt32();
                if (thisVersion != VERSION)
                {
                    OutputLog.Error("Version mismatch! Expected " + VERSION + " - Found " + thisVersion);
                    ret.Status = ParseResult.StatusCode.VersionMismatch;
                }
                else if (reader.BaseStream.Position == reader.BaseStream.Length)
                {
                    OutputLog.Log("No structure found at the given location.");
                    ret.Status = ParseResult.StatusCode.NotFound;
                }
                else
                {
                    List<string> files = ReadFiles(reader);
                    ret.Layout = ReadNode(reader, files);
                    FinalizeNode(ret.Layout);

                    OutputLog.Log("Found structure " + ret.Layout.Type + ".");
                    ret.Status = ParseResult.StatusCode.Found;
                }
            }
            
            fileStream.Close();
            return ret;
        }

        public async Task<ParseResult> ParseAsync(ProjectProperties projProperties, DocumentLocation location)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (location.Filename == null || location.Filename.Length == 0)
            {
                OutputLog.Error("No file provided for parsing");
                ParseResult ret = new ParseResult();
                ret.Status = ParseResult.StatusCode.InvalidInput;
                return ret;
            }

            string errorStr = CreateDirectory(OutputDirectory);

            if (errorStr != null)
            {
                ParseResult ret = new ParseResult();
                ret.Status = ParseResult.StatusCode.InvalidOutputDir;
                ret.ParserLog = errorStr;
                return ret;
            }

            AdjustPaths(projProperties.IncludeDirectories);
            AdjustPaths(projProperties.ForceIncludes);

            string includes  = GenerateCommandStr("-I",projProperties.IncludeDirectories);
            string forceInc  = GenerateCommandStr("-include", projProperties.ForceIncludes);
            string defines   = GenerateCommandStr("-D",projProperties.PrepocessorDefinitions);
            string workDir   = projProperties.WorkingDirectory.Length == 0 ? "" : " -working-directory=" + AdjustPath(projProperties.WorkingDirectory);
            string flags     = projProperties.ShowWarnings? "" : " -w";
            string extra     = projProperties.ExtraArguments.Length == 0? "" : " " + projProperties.ExtraArguments;

            string standard  = GetStandardFlag(projProperties.Standard);
            string language  = Path.GetExtension(location.Filename) == ".c"? "" : " -x c++"; //do not force c++ on .c files 
            string archStr   = projProperties != null && projProperties.Target == ProjectProperties.TargetType.x86 ? " -m32" : " -m64";

            string clangCmd = language + archStr + standard + flags + defines + includes + forceInc + workDir + extra;

            string outputPath = OutputDirectory + @"tempResult.slbin";
            string contextCmd = AdjustPath(location.Filename) + " -r=" + location.Line + " -c=" + location.Column + " -o=" + AdjustPath(outputPath);

            string toolCmd = contextCmd + " --" + clangCmd;

            OutputLog.Focus();
            OutputLog.Log("Looking for structures at " + location.Filename + ":" + location.Line + ":" + location.Column+"...");

            if (PrintCommandLine)
            {
                OutputLog.Log("CLANG ARGUMENTS: " + clangCmd);
                //OutputLog.Log("GENERATED FILE: " + outputPath);
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();

            ExternalProcess externalProcess = new ExternalProcess();
            externalProcess.Log = ""; //Set a valid log

            int exitCode = await externalProcess.ExecuteAsync(GetClangLayoutParserToolPath(), toolCmd);
            bool valid = exitCode == 0;

            watch.Stop();
            const long TicksPerMicrosecond = (TimeSpan.TicksPerMillisecond / 1000);
            string timeStr = " ("+GetTimeStr((ulong)(watch.ElapsedTicks / TicksPerMicrosecond))+")";

            if (!valid)
            {
                OutputLog.Error("Unable to scan the given location." + timeStr);
                ParseResult ret = new ParseResult();
                ret.Status = ParseResult.StatusCode.ParseFailed;
                ret.ParserLog = externalProcess.Log;
                return ret;
            }

            OutputLog.Log("File parsing completed! " + timeStr);
            return LoadParseResult(outputPath);
        }

        private string GetStandardFlag(ProjectProperties.StandardVersion standard)
        {
            switch (standard)
            {
                case ProjectProperties.StandardVersion.Cpp98:  return " -std=c++98";
                case ProjectProperties.StandardVersion.Cpp03:  return " -std=c++03";
                case ProjectProperties.StandardVersion.Cpp14:  return " -std=c++14";
                case ProjectProperties.StandardVersion.Cpp17:  return " -std=c++17";
                case ProjectProperties.StandardVersion.Cpp20:  return " -std=c++20";
                case ProjectProperties.StandardVersion.Gnu98:  return " -std=gnu++98";
                case ProjectProperties.StandardVersion.Gnu03:  return " -std=gnu++03";
                case ProjectProperties.StandardVersion.Gnu14:  return " -std=gnu++14";
                case ProjectProperties.StandardVersion.Gnu17:  return " -std=gnu++17";
                case ProjectProperties.StandardVersion.Gnu20:  return " -std=gnu++20";
                case ProjectProperties.StandardVersion.Latest: return " -std=c++2a";
                default: return "";
            }
        }

        private string AdjustPath(string input)
        {
            return input.Contains(' ')? '"' + input + '"' : input;
        }

        private void AdjustPaths(List<string> list)
        {
            for(int i=0;i<list.Count;++i)
            {
                list[i] = AdjustPath(list[i]);
            }
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

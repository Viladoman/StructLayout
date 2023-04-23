using EnvDTE;
using Microsoft.VisualStudio.Shell;

/*
#if VS16
//TODO ~ ramonv ~ Figure this out as this assembly is not part of the SDK and I can't compile both in 1
//We will have to split in 2 solutions
using Microsoft.VisualStudio.VCCodeModel;
#endif
*/
namespace StructLayout
{
    public class CodeModelLayout
    {
        static public CodeElement FindCodeElementAtLocation(DocumentLocation location)
        {
            //Use VS intellisense data to find the struct scope and store the first line before querying the pdb information
            ThreadHelper.ThrowIfNotOnUIThread();
            Document activeDocument = EditorUtils.GetActiveDocument();
            ProjectItem projItem = activeDocument == null ? null : activeDocument.ProjectItem;
            FileCodeModel model = activeDocument == null ? null : projItem.FileCodeModel;
            CodeElements globalElements = model == null ? null : model.CodeElements;
            return FindCodeElementAtLocation(globalElements, location.Line, location.Column);
        }

        static private CodeElement FindCodeElementAtLocation(CodeElements elements, uint line, uint column)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            //TODO ~ ramonv ~ find structures inside functions 

            if (elements != null)
            {
                foreach (CodeElement element in elements)
                {
                    TextPoint elementStart = element.StartPoint;
                    TextPoint elementEnd = element.EndPoint;
                    if (line >= elementStart.Line && line <= elementEnd.Line)
                    {
                        switch (element.Kind)
                        {
                            case vsCMElement.vsCMElementClass:
                                {
                                    CodeElement foundSubElement = FindCodeElementAtLocation(((CodeClass)element).Members, line, column);
                                    return foundSubElement == null ? element : foundSubElement;
                                }
                            case vsCMElement.vsCMElementStruct:
                                {
                                    CodeElement foundSubElement = FindCodeElementAtLocation(((CodeStruct)element).Members, line, column);
                                    return foundSubElement == null ? element : foundSubElement;
                                }
                            case vsCMElement.vsCMElementUnion:
                                {
                                    CodeElement foundSubElement = FindCodeElementAtLocation(element.Children, line, column);
                                    return foundSubElement == null ? element : foundSubElement;
                                }
                            case vsCMElement.vsCMElementNamespace:
                                {
                                    return FindCodeElementAtLocation(((CodeNamespace)element).Members, line, column);
                                }
                        }
                    }
                }
            }

            return null;
        }
/*
#if VS17
        //TODO ~ ramonv ~ placeholder while compiling
        public static LayoutNode ExtractLayout(CodeElement element)
        { 
            return null;
        }
#else
        private static LayoutNode ComputeType(CodeTypeRef typeRef)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (typeRef == null)
            {
                return null;
            }


            vsCMTypeRef childTypeKind = typeRef.TypeKind;

            string typename = typeRef.AsFullName;

            if (childTypeKind == vsCMTypeRef.vsCMTypeRefCodeType)
            {
                //ComplexField
                return ComputeStruct(typeRef.CodeType);
            }

            LayoutNode node = new LayoutNode();

            //TODO ~ ElementType returns the type of the elements of this array type if TypeKind is vsCMTypeRefArray. This may be Nothing for languages that have a default type.

            //TODO~  ramonv ~ fill
            node.Type = typeRef.AsFullName;
            node.Size = 1;
            node.Align = 1;

            return node;
        }  

        private static LayoutNode ComputeStruct(CodeType elementType)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (elementType == null)
            {
                return null;
            }

            CodeElements bases; 
            CodeElements members; 

            //Get proper VCCode 
            if (elementType.Kind == vsCMElement.vsCMElementStruct)
            {
                VCCodeStruct structElement = (VCCodeStruct)elementType;
                bases = structElement.Bases;
                members = structElement.Members;
                
            }
            else if (elementType.Kind == vsCMElement.vsCMElementUnion || elementType.Kind == vsCMElement.vsCMElementClass )
            {
                VCCodeClass classElement = (VCCodeClass)elementType;
                bases = classElement.Bases;
                members = classElement.Members;
            }
            else
            {
                //Unhandled type
                return null;
            }

            
            LayoutNode node = new LayoutNode();
            node.Type = elementType.FullName;
            //node.Name = reader.ReadString();

            node.Offset = 0;
            node.Category = LayoutNode.LayoutCategory.ComplexField;

            uint fakeOffset = 0;

            //traverse bases
            foreach (CodeElement parent in bases)
            {
                VCCodeBase parentRelation = (VCCodeBase)parent;
                if (parentRelation.IsVirtual)
                {
                    //TODO ~ Ramonv ~ act on virtual base
                    LayoutNode baseNode = ComputeStruct(parentRelation.Class);
                    baseNode.Category = LayoutNode.LayoutCategory.VBase;
                    baseNode.Offset = fakeOffset;
                    fakeOffset += baseNode.Size;
                    node.AddChild(baseNode);
                }
                else
                {
                    LayoutNode baseNode = ComputeStruct(parentRelation.Class);
                    baseNode.Category = LayoutNode.LayoutCategory.NVBase;
                    baseNode.Offset = fakeOffset;
                    fakeOffset += baseNode.Size;
                    node.AddChild(baseNode);
                }
            }

            //traverse members
            foreach ( CodeElement member in members)
            {
                if (member.Kind == vsCMElement.vsCMElementVariable )
                {
                    VCCodeVariable child = (VCCodeVariable)member;
                    if (!child.IsShared)
                    {
                        //non static member
                        LayoutNode childNode = ComputeType(child.Type);
                        childNode.Name = child.Name;
                        childNode.Offset = fakeOffset;
                        fakeOffset += childNode.Size;
                        node.AddChild(childNode);
                    }
                }
            }

            //TODO ~ ramonv ~ compute size and alignment based on components
            node.Size = fakeOffset;
            node.Align = 8;

            return node;

            //TODO ~ ramonv ~ collect bases
            //TODO ~ ramonv ~ store virtual bases
            //TODO ~ ramonv ~ check virtual presence
            //TODO ~ ramonv ~ check bitfields 
            //TODO ~ Ramovn ~ check nested structs 

            
      if (element.Kind == vsCMElement.vsCMElementVariable)
      {

          //VCCodeVariable variable = element as VCCodeVariable;
          CodeVariable variable = element as CodeVariable;
          CodeTypeRef t = variable.Type;
          string hello = t.AsFullName;
          OutputLog.Log(hello);

      }
      
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

        public static LayoutNode ExtractLayout(CodeElement element)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            VCCodeElement vcElement = element as VCCodeElement;

            if (vcElement == null || !vcElement.IsCodeType)
            {
                return null;
            }

            //TODO ~ ramonv ~ compute only leaf sizes,types, names and locations and add all vptrs as needed
            //TODO ~ ramonv ~ do a pass to remove unneeded vptrs.
            //TODO ~ ramonv ~ pass and set alignment and offsets. 
            //TODO ~ ramonv ~ fix virtual bases

            return ComputeStruct((CodeType)vcElement);
        }
#endif
*/
    }
}

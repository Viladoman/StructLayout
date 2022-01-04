#include <algorithm>

#include "IO.h"
#include "LayoutDefinitions.h"

#include "dia2.h" 
#include "diacreate.h"

namespace PDBReader
{
    namespace Helpers
    {
        // -----------------------------------------------------------------------------------------------------------
        template<typename T> T Min(T a, T b) { return a > b ? b : a; }
        template<typename T> T Max(T a, T b) { return a > b ? a : b; }

        // -----------------------------------------------------------------------------------------------------------
        template<typename T>
        unsigned GetTrailingZeroes(T x)
        {
            if (x == 0)
            {
                return sizeof(T) * 8;
            }
            unsigned bits = 0;
            for (; (x & 1) == 0; ++bits, x >>= 1) {}
            return bits;
        }

        // -----------------------------------------------------------------------------------------------------------
        IDiaEnumSymbols* FindChildren(IDiaSymbol* symbol, enum SymTagEnum symTag)
        {
            IDiaEnumSymbols* children;
            return symbol && symbol->findChildrenEx(symTag, nullptr, nsNone, &children) == S_OK ? children : nullptr;
        }

        // -----------------------------------------------------------------------------------------------------------
        template<typename OBJECT, typename T, typename R>
        R* Next(T* enumeration, HRESULT(OBJECT::* TNextFunction)(ULONG, R**, ULONG*))
        {
            R* next = nullptr;
            unsigned long fetched = 0;
            return enumeration && (enumeration->*TNextFunction)(1, &next, &fetched) == S_OK && fetched == 1 ? next : nullptr;
        }

        // -----------------------------------------------------------------------------------------------------------
        template< typename R, typename OBJECT >
        R QueryDIAFunction(OBJECT* obj, HRESULT(OBJECT::* TFunctionName)(R*))
        {
            R a;
            if (obj && (obj->*TFunctionName)(&a) == S_OK)
            {
                return a;
            }
            return R();
        }

        // -----------------------------------------------------------------------------------------------------------
        bool SameFilename(const wchar_t* a, const wchar_t* b)
        {
            if (a && b)
            {
                for (; *a != L'\0' && (*a == *b || (*a == L'/' && *b == L'\\') || (*a == L'\\' && *b == L'/')); ++a, ++b) {}
                return *a == L'\0' && *b == L'\0';
            }
            return false;
        }

        // -----------------------------------------------------------------------------------------------------------
        std::string wchar2string(const wchar_t* str)
        {
            std::string ret;
            if (str)
            {
                while (*str) { ret += (char)*str++; }
            }
            return  ret;
        }

        // -----------------------------------------------------------------------------------------------------------
        Layout::TAmount GetArchitecturePointerSize(DWORD machineType)
        {
            switch (machineType)
            {
                //32-bit
            case IMAGE_FILE_MACHINE_MIPS16:    //MIPS16 (16-bit instruction codes, 8to32bit bus)- Microprocessor without Interlocked Pipeline Stages Architecture
            case IMAGE_FILE_MACHINE_MIPSFPU16: //MIPS16 with FPU (Floating Point Unit aka a math co-processesor)(16-bit instruction codes, 8to32bit bus)
            case IMAGE_FILE_MACHINE_ALPHA:     //Digital Equipment Corporation (DEC) Alpha (32-bit)
            case IMAGE_FILE_MACHINE_AM33:      //Matsushita AM33, now MN103 (32-bit) part of Panasonic Corporation
            case IMAGE_FILE_MACHINE_ARM:       //ARM little endian (32-bit), ARM Holdings, later versions 6+ used in iPhone, Microsoft Nokia N900
            case IMAGE_FILE_MACHINE_EBC:       //EFI byte code (32-bit), now (U)EFI or (Unified) Extensible Firmware Interface
            case IMAGE_FILE_MACHINE_I386:      //Intel 386 or later processors and compatible processors (32-bit)
            case IMAGE_FILE_MACHINE_M32R:      //Mitsubishi M32R little endian (32-bit) now owned by Renesas Electronics Corporation
            case IMAGE_FILE_MACHINE_MIPSFPU:   //MIPS with FPU, MIPS Technologies (32-bit)
            case IMAGE_FILE_MACHINE_POWERPC:   //Power PC little endian, Performance Optimization With Enhanced RISC – Performance Computing (32-bit) one of the first
            case IMAGE_FILE_MACHINE_POWERPCFP: //Power PC with floating point support (FPU) (32-bit), designed by AIM Alliance (Apple, IBM, and Motorola)
            case IMAGE_FILE_MACHINE_R3000:     //R3000 (32-bit) RISC processor
            case IMAGE_FILE_MACHINE_SH3:       //Hitachi SH-3 (32-bit) - SuperH processor (SH3) core family
            case IMAGE_FILE_MACHINE_SH3DSP:    //Hitachi SH-3 DSP (32-bit)
            case IMAGE_FILE_MACHINE_SH3E:      //SH3E little-endian (32-bit)
            case IMAGE_FILE_MACHINE_SH4:       //Hitachi SH-4 (32-bit)
            case IMAGE_FILE_MACHINE_TRICORE:   //Infineon AUDO (Automotive unified processor) (32-bit) - Tricore architecture a unified RISC/MCU/DSP microcontroller core
            case IMAGE_FILE_MACHINE_THUMB:     //ARM or Thumb (interworking), (32-bit) core instruction set, used in Nintendo Gameboy Advance
                return 4;

                //64-bit
            case IMAGE_FILE_MACHINE_TARGET_HOST: // Useful for indicating we want to interact with the host and not a WoW guest.
            case IMAGE_FILE_MACHINE_AMD64:       //AMD (64-bit) - Advanced Micro Devices oR IS IT ??? OVERLOADED _AMD64 = 0x8664 - http://msdn.microsoft.com/en-us/library/windows/desktop/ms680313(v=vs.85).aspx  
            case IMAGE_FILE_MACHINE_ARM64:       //ARM8+ (64-bit)
            case IMAGE_FILE_MACHINE_IA64:        //Intel Itanium architecture processor family, (64-bit)
            case IMAGE_FILE_MACHINE_R4000:       //R4000 MIPS (64-bit) - claims to be first true 64-bit processor
            case IMAGE_FILE_MACHINE_R10000:      //R10000 MIPS IV is a (64-bit) architecture, but the R10000 did not implement the entire physical or virtual address to reduce cost. Instead, it has a 40-bit physical address and a 44-bit virtual address, thus it is capable of addressing 1 TB of physical memory and 16 TB of virtual memory. These comments by metadataconsulting.ca
            case IMAGE_FILE_MACHINE_SH5:         //Hitachi SH-5, (64-bit) core with a 128-bit vector FPU (64 32-bit registers) and an integer unit which includes the SIMD support and 63 64-bit registers.
            case IMAGE_FILE_MACHINE_ALPHA64:     //DEC Alpha AXP (64-bit) or IMAGE_FILE_MACHINE_AXP64
                return 8;

            case IMAGE_FILE_MACHINE_ARMNT:       // ARM Thumb-2 Little-Endian
            case IMAGE_FILE_MACHINE_CEF:
            case IMAGE_FILE_MACHINE_CEE:
            case IMAGE_FILE_MACHINE_WCEMIPSV2:   //MIPS Windows Compact Edition v2
            case IMAGE_FILE_MACHINE_UNKNOWN:
            default:
                LOG_WARNING("Could not find the machine pointer bit size for machine image number %u ( assumed 64bit - this only affects virtual base table pointer size )", machineType);
                return 8;
            }
        }

        // -----------------------------------------------------------------------------------------------------------
        bool IsLayoutFreeAt(Layout::Node* node, Layout::TAmount offset, Layout::TAmount offsetEnd)
        {
            if (node->size < offsetEnd)
            {
                return false;
            }

            for (Layout::Node* child : node->children)
            {
                Layout::TAmount childEnd = (child->offset + child->size);
                if ((offset >= child->offset && offset < childEnd) ||
                    (offsetEnd > child->offset && offsetEnd <= childEnd))
                {
                    return false;
                }
            }

            return true;
        }

        // -----------------------------------------------------------------------------------------------------------
        Layout::TAmount AlignOffsetTo(Layout::TAmount offset, Layout::TAmount alignment)
        {
            const unsigned int bits = GetTrailingZeroes(alignment);
            return ((offset + (alignment - 1)) >> bits) << bits;
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    struct SessionContext
    {
        IDiaSession* session = nullptr;
        IDiaSymbol* globalScope = nullptr;
        Layout::TAmount pointerSize = 8;
    };

    // -----------------------------------------------------------------------------------------------------------
    SessionContext OpenPDBSession(const wchar_t* filename)
    {
        SessionContext ret;
        IDiaDataSource* source = nullptr;

        if (NoOleCoCreate(CLSID_DiaSourceAlt, IID_IDiaDataSource, (void**)(&source)) < 0)
        {
            // We were not able to find the dia library on the registry try to find it locally
            if (NoRegCoCreate(L"msdia140.dll", CLSID_DiaSourceAlt, IID_IDiaDataSource, (void**)(&source)) < 0)
            {
                LOG_ERROR("Unable to find the msdia140.dll on the registry or locally.");
                return ret;
            }
        }

        if (source->loadDataFromPdb(filename) < 0)
        {
            LOG_ERROR("Failed to load the pdb file.");
            return ret;
        }

        if (source->openSession(&ret.session) < 0)
        {
            LOG_ERROR("Failed to open the Dia Session.");
            return ret;
        }

        ret.globalScope = Helpers::QueryDIAFunction(ret.session, &IDiaSession::get_globalScope);
        ret.pointerSize = Helpers::GetArchitecturePointerSize(Helpers::QueryDIAFunction(ret.globalScope, &IDiaSymbol::get_machineType));

        return ret;
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////


    std::string GetTypeName(IDiaSymbol* type);

    // -----------------------------------------------------------------------------------------------------------
    const char* GetSimpleTypeName(IDiaSymbol* type)
    {
        const enum BasicType basicType = static_cast<enum BasicType>(Helpers::QueryDIAFunction(type, &IDiaSymbol::get_baseType));

        switch (basicType)
        {
        case btNoType:    return "";
        case btVoid:      return "void";
        case btChar:      return "char";
        case btWChar:     return "wchar_t";
        case btInt:
        {
            const long long size = Helpers::QueryDIAFunction(type, &IDiaSymbol::get_length);
            return size == 1 ? "int8" : size == 2 ? "int16" : size == 4 ? "int32" : size == 8 ? "int64" : size == 16 ? "int128" : "int???";
        }
        case btUInt:
        {
            const long long size = Helpers::QueryDIAFunction(type, &IDiaSymbol::get_length);
            return size == 1 ? "uint8" : size == 2 ? "uint16" : size == 4 ? "uint32" : size == 8 ? "uint64" : size == 16 ? "uint128" : "uint???";
        }
        case btFloat:
        {
            const long long size = Helpers::QueryDIAFunction(type, &IDiaSymbol::get_length);
            return size == 2 ? "half" : size == 4 ? "float" : size == 8 ? "double" : "float???";
        }
        case btBCD:       return "bcd";
        case btBool:      return "bool";
        case btLong:      return "long";
        case btULong:     return "unsigned long";
        case btCurrency:  return "currency";
        case btDate:      return "date";
        case btVariant:   return "variant";
        case btComplex:   return "complex";
        case btBit:       return "bit";
        case btBSTR:      return "BSTR";
        case btHresult:   return "HRESULT";
        case btChar16:    return "char16_t";
        case btChar32:    return "char32_t";
        case btChar8:     return "char8_t";
        default: return "???";
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    std::string GetArrayTypeName(IDiaSymbol* type)
    {
        IDiaSymbol* innerType = Helpers::QueryDIAFunction(type, &IDiaSymbol::get_type);
        DWORD arrayCount = Helpers::QueryDIAFunction(type, &IDiaSymbol::get_count);

        if (!arrayCount)
        {
            const ULONGLONG typeSize = Helpers::QueryDIAFunction(innerType, &IDiaSymbol::get_length);
            if (!typeSize)
            {
                return "???[]";
            }

            const ULONGLONG arraySize = Helpers::QueryDIAFunction(type, &IDiaSymbol::get_length);
            arrayCount = static_cast<DWORD>(arraySize / typeSize);
        }

        return GetTypeName(innerType) + '[' + std::to_string(arrayCount) + ']';
    }

    // -----------------------------------------------------------------------------------------------------------
    std::string GetTypeName(IDiaSymbol* type)
    {
        std::string ret = "";

        if (!type)
        {
            return ret;
        }

        const enum SymTagEnum tag = static_cast<enum SymTagEnum>(Helpers::QueryDIAFunction(type, &IDiaSymbol::get_symTag));
        switch (tag)
        {
        case SymTagUDT:
        {
            const enum UdtKind kind = static_cast<enum UdtKind>(Helpers::QueryDIAFunction(type, &IDiaSymbol::get_udtKind));
            ret = (kind == UdtUnion ? "union " : "") + Helpers::wchar2string(Helpers::QueryDIAFunction(type, &IDiaSymbol::get_name));
        }
        break;
        case SymTagPointerType:
        {
            //nest the inner type
            ret = GetTypeName(Helpers::QueryDIAFunction(type, &IDiaSymbol::get_type)) + (Helpers::QueryDIAFunction(type, &IDiaSymbol::get_reference) ? "&" : "*");

            //add decorations
            if (Helpers::QueryDIAFunction(type, &IDiaSymbol::get_unalignedType))
            {
                ret = "__unaligned " + ret;
            }

            if (Helpers::QueryDIAFunction(type, &IDiaSymbol::get_volatileType))
            {
                ret = "volatile " + ret;
            }

            if (Helpers::QueryDIAFunction(type, &IDiaSymbol::get_constType))
            {
                ret = "const " + ret;
            }
        }
        break;
        case SymTagArrayType:
        {
            ret = GetArrayTypeName(type);
        }
        break;
        case SymTagEnum:
        {
            ret = "enum " + Helpers::wchar2string(Helpers::QueryDIAFunction(type, &IDiaSymbol::get_name));
        }
        break;
        case SymTagBaseType:
        {
            ret = GetSimpleTypeName(type);
        }
        break;
        default:
            break;
        }

        return ret;
    }

    // -----------------------------------------------------------------------------------------------------------
    Layout::TAmount GuessAlignment(Layout::Node* node, IDiaSymbol* type)
    {
        const Layout::TAmount maxOffsetAlign = node->offset == 0 ? 1024 : (1 << Helpers::GetTrailingZeroes(node->offset));

        const enum SymTagEnum tag = static_cast<enum SymTagEnum>(Helpers::QueryDIAFunction(type, &IDiaSymbol::get_symTag));
        switch (tag)
        {
        case SymTagUDT:
        {
            Layout::TAmount align = 1;
            for (Layout::Node* childNode : node->children)
            {
                align = Helpers::Max(align, childNode->align);
            }
            const Layout::TAmount typeSize = Helpers::QueryDIAFunction(type, &IDiaSymbol::get_length);
            return Helpers::Min(maxOffsetAlign, Helpers::Max(Layout::TAmount(1u), Helpers::Min(align, typeSize)));
        }
        case SymTagArrayType:
            return GuessAlignment(node, Helpers::QueryDIAFunction(type, &IDiaSymbol::get_type));

        case SymTagEnum:
        case SymTagBaseType:
        case SymTagPointerType:
        default:
        {
            const Layout::TAmount typeSize = Helpers::QueryDIAFunction(type, &IDiaSymbol::get_length);
            return Helpers::Min(maxOffsetAlign, Helpers::Max(Layout::TAmount(1u), typeSize));
        }
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    struct TypeContext
    {
        std::vector<Layout::Node*> virtualBases;
    };

    // -----------------------------------------------------------------------------------------------------------
    bool ContainerHasNode(const std::vector<Layout::Node*>& container, Layout::Node* input)
    {
        if (input && !input->type.empty())
        {
            for (Layout::Node* node : container)
            {
                if (node->type == input->type)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // -----------------------------------------------------------------------------------------------------------
    void ContainerAddNode(std::vector<Layout::Node*>& container, Layout::Node* input)
    {
        if (input && !ContainerHasNode(container, input))
        {
            container.emplace_back(input);
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    void InjectVBTablePtr(const SessionContext& sessionContext, Layout::Node* node)
    {
        Layout::TAmount tentativeOffset = 0;

        //find the memory position just after the non virtual bases
        auto placement = node->children.begin();
        for (; placement != node->children.end(); ++placement)
        {
            if ((*placement)->nature != Layout::Category::NVBase)
            {
                break;
            }
            tentativeOffset = (*placement)->offset + (*placement)->size;
        }

        tentativeOffset = Helpers::AlignOffsetTo(tentativeOffset, sessionContext.pointerSize);
        if (Helpers::IsLayoutFreeAt(node, tentativeOffset, tentativeOffset + sessionContext.pointerSize))
        {
            //Add the virtual base offset pointer
            Layout::Node* fieldNode = new Layout::Node();
            fieldNode->nature = Layout::Category::VBTablePtr;
            fieldNode->offset = tentativeOffset;
            fieldNode->size = sessionContext.pointerSize;
            fieldNode->align = fieldNode->size;
            node->children.emplace(placement, fieldNode);
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    void RemoveVirtualBasesFromNode(const SessionContext& sessionContext, TypeContext& typeContext, Layout::Node* node, std::vector<Layout::Node*>& virtualBases)
    {
        //try to injecet the vbTablePtr
        if (!virtualBases.empty())
        {
            //remove the vbases size from the structure as it is counted as a type but we should only have in on the root node
            Layout::TAmount vbasesSize = 0u;
            for (Layout::Node* child : typeContext.virtualBases)
            {
                //follow the typecontext order, as this dictates the final order in the struct. 
                if (ContainerHasNode(virtualBases, child))
                {
                    vbasesSize = Helpers::AlignOffsetTo(vbasesSize, child->align) + child->size;
                }
            }
            vbasesSize = Helpers::AlignOffsetTo(vbasesSize, sessionContext.pointerSize);
            node->size -= vbasesSize;

            //With the new size restriction try to inject the VBTablePtr
            InjectVBTablePtr(sessionContext, node);
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    Layout::Node* ComputeTypeRecursive(const SessionContext& sessionContext, TypeContext& typeContext, IDiaSymbol* type)
    {
        if (type == nullptr)
        {
            return nullptr;
        }

        Layout::Node* node = new Layout::Node();

        node->type   = GetTypeName(type);
        node->size   = Helpers::QueryDIAFunction(type, &IDiaSymbol::get_length);

        std::vector<Layout::Node*> thisVirtualBases;

        IDiaEnumSymbols* children = Helpers::FindChildren(type, SymTagNull);
        while (IDiaSymbol* child = Helpers::Next(children, &IDiaEnumSymbols::Next))
        {
            const enum SymTagEnum tag = static_cast<enum SymTagEnum>(Helpers::QueryDIAFunction(child, &IDiaSymbol::get_symTag));

            if (tag == SymTagNull     || 
                tag == SymTagFunction ||
                tag == SymTagFriend   ||
                tag == SymTagEnum     ||
                tag == SymTagUDT      ||
                tag == SymTagBaseType ||
                tag == SymTagTypedef )
            { 
                continue;
            }

            if (tag == SymTagBaseClass)
            {
                IDiaSymbol* baseType = Helpers::QueryDIAFunction(child, &IDiaSymbol::get_type);
                Layout::Node* baseNode = ComputeTypeRecursive(sessionContext, typeContext, baseType);

                if (Helpers::QueryDIAFunction(child, &IDiaSymbol::get_virtualBaseClass))
                {
                    //virtual base
                    baseNode->nature = Layout::Category::VBase;
                    ContainerAddNode(typeContext.virtualBases, baseNode);
                    thisVirtualBases.emplace_back(baseNode);
                }
                else
                {
                    //Non virtual base
                    baseNode->offset = Helpers::QueryDIAFunction(child, &IDiaSymbol::get_offset);
                    baseNode->nature = Layout::Category::NVBase; 
                    node->children.emplace_back(baseNode);
                }
                
            }
            else
            {
                enum LocationType locationType = static_cast<enum LocationType>(Helpers::QueryDIAFunction(child, &IDiaSymbol::get_locationType));
                if (locationType == LocIsThisRel || locationType == LocIsNull || locationType == LocIsBitField)
                {
                    // TODO ~ ramonv ~ missing location extraction

                    IDiaSymbol* childType = Helpers::QueryDIAFunction(child, &IDiaSymbol::get_type);
                    const enum SymTagEnum childTag = static_cast<enum SymTagEnum>(Helpers::QueryDIAFunction(childType, &IDiaSymbol::get_symTag));
                        
                    if (childTag == SymTagUDT)
                    {
                        //complex field
                        Layout::Node* fieldNode = ComputeTypeRecursive(sessionContext, typeContext, childType);
                        fieldNode->name = Helpers::wchar2string(Helpers::QueryDIAFunction(child, &IDiaSymbol::get_name));
                        fieldNode->offset = Helpers::QueryDIAFunction(child, &IDiaSymbol::get_offset);
                        fieldNode->nature = Layout::Category::ComplexField;
                        fieldNode->align  = GuessAlignment(fieldNode, childType);

                        node->children.emplace_back(fieldNode);
                    }
                    else
                    {
                        Layout::Node* fieldNode = new Layout::Node();

                        IDiaSymbol* childType = Helpers::QueryDIAFunction(child, &IDiaSymbol::get_type);

                        fieldNode->name   = Helpers::wchar2string(Helpers::QueryDIAFunction(child, &IDiaSymbol::get_name));
                        
                        fieldNode->type   = GetTypeName(childType);
                        fieldNode->nature = Layout::Category::SimpleField;
                        
                        fieldNode->offset = Helpers::QueryDIAFunction(child, &IDiaSymbol::get_offset);
                        fieldNode->size   = Helpers::QueryDIAFunction(childType, &IDiaSymbol::get_length);
                        fieldNode->align  = GuessAlignment(fieldNode, childType);

                        if (childTag == SymTagPointerType)
                        {
                            //Check for vtablePtr
                            IDiaSymbol* ptrType = Helpers::QueryDIAFunction(childType, &IDiaSymbol::get_type);
                            const enum SymTagEnum ptrTag = static_cast<enum SymTagEnum>(Helpers::QueryDIAFunction(ptrType, &IDiaSymbol::get_symTag));
                            if (ptrTag == SymTagVTable || ptrTag == SymTagVTableShape)
                            {
                                fieldNode->type   = "";
                                fieldNode->nature = Layout::Category::VTablePtr;
                            }
                                
                            fieldNode->align = fieldNode->size;
                        }

                        if (locationType == LocIsBitField)
                        {
                            fieldNode->nature = Layout::Category::Bitfield;

                            Layout::Node* extraData = new Layout::Node();
                            extraData->offset = Helpers::QueryDIAFunction(child, &IDiaSymbol::get_bitPosition);
                            extraData->size = Helpers::QueryDIAFunction(child, &IDiaSymbol::get_length);

                            fieldNode->children.emplace_back(extraData);
                        }

                        node->children.emplace_back(fieldNode);

                    }
                }
            }
        }

        std::stable_sort(node->children.begin(), node->children.begin(), [](Layout::Node* a, Layout::Node* b) { return a->offset < b->offset; });

        RemoveVirtualBasesFromNode(sessionContext, typeContext, node, thisVirtualBases);
        
        node->align = GuessAlignment(node, type);
        return node;
    }

    // -----------------------------------------------------------------------------------------------------------
    void FixVirtualBases(const SessionContext& sessionContext, const TypeContext typeContext, Layout::Node* node, IDiaSymbol* type)
    {
        if (node && !typeContext.virtualBases.empty())
        {
            //Add all the found virtual bases at the end of the structure
            for (Layout::Node* vb : typeContext.virtualBases)
            {
                vb->offset = Helpers::AlignOffsetTo(node->size, vb->align);
                node->size = vb->offset + vb->size;
                node->children.emplace_back(vb);
            }
            node->size = Helpers::AlignOffsetTo(node->size, sessionContext.pointerSize);

            //restore the OG node size 
            const Layout::TAmount correctSize = Helpers::QueryDIAFunction(type, &IDiaSymbol::get_length);
            if (correctSize != node->size)
            {
                LOG_WARNING("Found different struct sizes constructing the virutal bases: got %d and expected %d from the queried type. The layout might have mistakes!", node->size, correctSize);
            }
            node->size = correctSize;
            node->align = GuessAlignment(node, type); //re-guess alignment as the virtual bases might have changed the overall alignment
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    Layout::Node* ComputeType(const SessionContext& context, IDiaSymbol* type)
    {
        TypeContext typeContext;
        Layout::Node* node = ComputeTypeRecursive(context, typeContext, type);
        FixVirtualBases(context, typeContext, node, type);
        return node;
    }

    // -----------------------------------------------------------------------------------------------------------
    IDiaSymbol* FindSymbolAtLocation(const SessionContext& context, const wchar_t* filename, const DWORD line)
    {
        unsigned int totalUdtCount = 0u;

        IDiaEnumSymbols* children = Helpers::FindChildren(context.globalScope, SymTagUDT);
        while (IDiaSymbol* child = Helpers::Next(children, &IDiaEnumSymbols::Next))
        {
            ++totalUdtCount;

            IDiaLineNumber* location = Helpers::QueryDIAFunction(child, &IDiaSymbol::getSrcLineOnTypeDefn);
            const DWORD     lineNumber = Helpers::QueryDIAFunction(location, &IDiaLineNumber::get_lineNumber);
            IDiaSourceFile* childFile = Helpers::QueryDIAFunction(location, &IDiaLineNumber::get_sourceFile);
            const DWORD     childFileId = Helpers::QueryDIAFunction(childFile, &IDiaSourceFile::get_uniqueId);
            const wchar_t* childFilename = Helpers::QueryDIAFunction(childFile, &IDiaSourceFile::get_fileName);

            if (location && childFilename && lineNumber == line && Helpers::SameFilename(childFilename, filename))
            {
                return child;
            }
        }

        if (totalUdtCount == 0)
        {
            LOG_WARNING("There were no User Defined Types found in the input symbol database.");
        }

        return nullptr;
    }

    // -----------------------------------------------------------------------------------------------------------
    bool ExportResult(Layout::Result& result, const wchar_t* outputPath)
    {
        const std::string outputStr = Helpers::wchar2string(outputPath);
        const char* outputFileName = outputStr.size() == 0 ? "output.slbin" : outputStr.c_str();
        return IO::ToFile(result, outputFileName);
    }

    // -----------------------------------------------------------------------------------------------------------
    bool ExportAtLocation(const wchar_t* pdbFile, const wchar_t* filename, const int line, const wchar_t* outputPath)
	{
        if (!pdbFile)
        {
            LOG_ERROR("No pdb file path provided.");
            return false;
        }

        if (!outputPath)
        {
            LOG_ERROR("No output file path provided.");
            return false;
        }

        if (!filename)
        {
            LOG_ERROR("No location file path provided.");
            return false;
        }

        SessionContext context = OpenPDBSession(pdbFile);

        if (!context.session || !context.globalScope)
        {
            return false;
        }

        Layout::Result result;
        IDiaSymbol* symbol = FindSymbolAtLocation(context, filename, line);
        result.node = ComputeType(context, symbol);

        return ExportResult(result, outputPath);
	}
}
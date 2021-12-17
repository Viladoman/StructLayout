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
            return obj && (obj->*TFunctionName)(&a) == S_OK ? a : R();
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
    }

    // -----------------------------------------------------------------------------------------------------------
    IDiaSession* OpenPDBSession(const wchar_t* filename)
    {
        IDiaSession* session = nullptr;
        IDiaDataSource* source = nullptr;

        if ( NoOleCoCreate( CLSID_DiaSourceAlt, IID_IDiaDataSource, (void**)(&source) ) < 0 )
        {
            // We were not able to find the dia library on the registry try to find it locally
            if ( NoRegCoCreate(L"msdia140.dll", CLSID_DiaSourceAlt, IID_IDiaDataSource, (void**)(&source) ) < 0)
            {
                LOG_ERROR( "Unable to find the msdia140.dll on the registry or locally.");
                return nullptr;
            }
        }

        if ( source->loadDataFromPdb( filename ) < 0 )
        {
            LOG_ERROR("Failed to load the pdb file.");
            return nullptr;
        }

        if ( source->openSession( &session ) < 0 )
        {
            LOG_ERROR( "Failed to open the Dia Session.");
            return nullptr;
        }

        return session;
    }

	//////////////////////////////////////////////////////////////////////////////////////////////////////////////

    std::string GetTypeName(IDiaSymbol* type);

    // -----------------------------------------------------------------------------------------------------------
    IDiaSymbol* FindSymbolAtLocation( IDiaSession* session, IDiaSymbol* symbol, const wchar_t* filename, const DWORD line )
    {
        //TODO ~ ramonv ~ find a way to better retrieve this

        IDiaSymbol* ret = nullptr;

        IDiaEnumSymbols* children = Helpers::FindChildren(symbol, SymTagUDT);
        while (IDiaSymbol* child = Helpers::Next(children,&IDiaEnumSymbols::Next))
        {
            IDiaLineNumber* location      = Helpers::QueryDIAFunction(child,     &IDiaSymbol::getSrcLineOnTypeDefn);
            const DWORD     lineNumber    = Helpers::QueryDIAFunction(location,  &IDiaLineNumber::get_lineNumber);
            IDiaSourceFile* childFile     = Helpers::QueryDIAFunction(location,  &IDiaLineNumber::get_sourceFile);
            const DWORD     childFileId   = Helpers::QueryDIAFunction(childFile, &IDiaSourceFile::get_uniqueId);
            const wchar_t*  childFilename = Helpers::QueryDIAFunction(childFile, &IDiaSourceFile::get_fileName);

            if (location && childFilename && lineNumber == line && Helpers::SameFilename(childFilename, filename))
            {
                return child;
            }           
        }
            
        return ret;
    }

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
            return size == 1? "uint8" : size == 2 ? "uint16" : size == 4 ? "uint32" : size == 8 ? "uint64" : size == 16? "uint128" : "uint???";
        }
        case btFloat:     
        {
            const long long size = Helpers::QueryDIAFunction(type, &IDiaSymbol::get_length);
            return size == 2? "half" : size == 4? "float" : size == 8? "double" : "float???";
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
            const ULONGLONG typeSize  = Helpers::QueryDIAFunction(innerType, &IDiaSymbol::get_length);
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
                    ret = (kind == UdtUnion? "union " : "") + Helpers::wchar2string(Helpers::QueryDIAFunction(type, &IDiaSymbol::get_name));
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
        //TODO ~ ramonv ~ char array[MAX_] fails ( validate with size too )

        const Layout::TAmount maxOffsetAlign = node->offset == 0? 1024 : (1 << Helpers::GetTrailingZeroes(node->offset));

        const enum SymTagEnum tag = static_cast<enum SymTagEnum>(Helpers::QueryDIAFunction(type, &IDiaSymbol::get_symTag));
        switch (tag)
        {      
        case SymTagUDT:
        {
            Layout::TAmount align = 1;
            for (Layout::Node* childNode : node->children)
            {
                align = Helpers::Max(align,childNode->align);
            }
            return Helpers::Min(maxOffsetAlign, Helpers::Max(Layout::TAmount(1u), Helpers::Min(align, node->size)));
        }
        case SymTagArrayType:
            return GuessAlignment(node, Helpers::QueryDIAFunction(type, &IDiaSymbol::get_type));

        case SymTagEnum:
        case SymTagBaseType:
        case SymTagPointerType:
        default:
            return Helpers::Min(maxOffsetAlign,Helpers::Max(Layout::TAmount(1u),node->size));
        }
    }

    // -----------------------------------------------------------------------------------------------------------
    Layout::Node* ComputeType(IDiaSymbol* type)
    {
        if (type == nullptr)
        {
            return nullptr;
        }

        Layout::Node* node = new Layout::Node();

        node->type   = GetTypeName(type);
        node->size   = Helpers::QueryDIAFunction(type, &IDiaSymbol::get_length);

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
                //TODO ~ ramonv ~ test isvirtualinheritance...

                IDiaSymbol* baseType = Helpers::QueryDIAFunction(child, &IDiaSymbol::get_type);
                Layout::Node* baseNode = ComputeType(baseType);
                baseNode->offset = Helpers::QueryDIAFunction(child, &IDiaSymbol::get_offset);
                baseNode->nature = Layout::Category::NVBase; //TODO ~ ramonv ~ revisist this check virtual bases
                node->children.push_back(baseNode);
            }
            else //if(!Helpers::QueryDIAFunction(child, &IDiaSymbol::get_isStatic))
            {
                enum LocationType locationType = static_cast<enum LocationType>(Helpers::QueryDIAFunction(child, &IDiaSymbol::get_locationType));
                if (locationType == LocIsThisRel || locationType == LocIsNull || locationType == LocIsBitField)
                {
                    // TODO ~ ramonv ~ missing location extraction and storage

                    IDiaSymbol* childType = Helpers::QueryDIAFunction(child, &IDiaSymbol::get_type);
                    const enum SymTagEnum childTag = static_cast<enum SymTagEnum>(Helpers::QueryDIAFunction(childType, &IDiaSymbol::get_symTag));
                        
                    if (childTag == SymTagUDT)
                    {
                        //complex field
                        Layout::Node* fieldNode = ComputeType(childType);
                        fieldNode->name = Helpers::wchar2string(Helpers::QueryDIAFunction(child, &IDiaSymbol::get_name));
                        fieldNode->offset = Helpers::QueryDIAFunction(child, &IDiaSymbol::get_offset);
                        fieldNode->nature = Layout::Category::ComplexField;
                        fieldNode->align  = GuessAlignment(fieldNode, childType);

                        node->children.push_back(fieldNode);
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

                            fieldNode->children.push_back(extraData);
                        }

                        node->children.push_back(fieldNode);

                    }
                }
            }
        }

        std::stable_sort(node->children.begin(), node->children.begin(), [](Layout::Node* a, Layout::Node* b) { return a->offset < b->offset; });

        node->align = GuessAlignment(node, type);
        return node;
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

        IDiaSession* session = OpenPDBSession(pdbFile);

        if (!session)
        {
            return false;
        }

        Layout::Result result;
        IDiaSymbol* symbol = FindSymbolAtLocation(session, Helpers::QueryDIAFunction(session, &IDiaSession::get_globalScope), filename, line);
        result.node = ComputeType(symbol);

        return ExportResult(result, outputPath);
	}
}
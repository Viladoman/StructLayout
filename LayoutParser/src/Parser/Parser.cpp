#ifdef _MSC_VER
#pragma warning(disable: 4146) // unary minus operator applied to unsigned type, result still unsigned
#endif

#include "Parser.h"

#pragma warning(push, 0)    

// Clang includes
#include <clang/AST/ASTConsumer.h>
#include <clang/AST/ASTContext.h>
#include <clang/AST/RecursiveASTVisitor.h>
#include <clang/AST/RecordLayout.h>
#include <clang/Analysis/CFG.h>
#include <clang/Basic/Diagnostic.h>
#include <clang/Basic/LangOptions.h>
#include <clang/Basic/TargetInfo.h>
#include <clang/Frontend/CompilerInstance.h>
#include <clang/Frontend/FrontendAction.h>
#include <clang/Tooling/CommonOptionsParser.h>
#include <clang/Tooling/Tooling.h>

// LLVM includes
#include <llvm/ADT/StringRef.h>
#include <llvm/Support/CommandLine.h>
#include <llvm/Support/raw_ostream.h>
#include <iostream>

#pragma warning(pop)    

#include "LayoutDefinitions.h"
#include "IO.h"

namespace ClangParser 
{
    namespace Helpers
    {
        inline bool IsMSLayout(const clang::ASTContext& context) { return context.getTargetInfo().getCXXABI().isMicrosoft(); }
    }

    // ----------------------------------------------------------------------------------------------------------
    class SLayouts
    { 
    public:

        void ComputeLayout(const clang::ASTContext& context, const clang::CXXRecordDecl* declaration)
        {
            if (!declaration || !declaration->getDefinition() || declaration->isInvalidDecl() || !declaration->isCompleteDefinition()) return;

            const clang::SourceManager& manager = context.getSourceManager();

            llvm::StringRef filename = manager.getFilename(declaration->getLocation());

            if (locationFilter.filename && filename == locationFilter.filename)
            { 
                const clang::SourceRange range = declaration->getSourceRange();
                const clang::PresumedLoc startLocation = manager.getPresumedLoc(range.getBegin());
                const clang::PresumedLoc endLocation = manager.getPresumedLoc(range.getEnd());

                const unsigned int startLine = startLocation.getLine();
                const unsigned int startCol  = startLocation.getColumn();
                const unsigned int endLine   = endLocation.getLine();
                const unsigned int endCol    = endLocation.getColumn();

                if ( (locationFilter.row > startLine || (locationFilter.row == startLine && locationFilter.col >= startCol)) && 
                     (locationFilter.row < endLine   || (locationFilter.row == endLine   && locationFilter.col <= endCol))   &&
                     (startLine > bestTree.startLine || (startLine == bestTree.startLine && startCol > bestTree.startCol)))
                { 
                    bestTree.root = ComputeStruct(context, declaration, true);
                    bestTree.startLine = startLine;
                    bestTree.startCol   = startCol;
                }
            }
        }

        void SetFilter(const Parser::LocationFilter& filter){ locationFilter = filter; }

        void Clear() { bestTree = Layout::Tree(); } //TODO ~ ramonv ~ this leaks memory 

        const Layout::Tree& GetLayout() const { return bestTree; }

    private:
        Layout::Node* ComputeStruct(const clang::ASTContext& context, const clang::CXXRecordDecl* declaration, const bool includeVirtualBases)
        {
            Layout::Node* node = new Layout::Node();

            const clang::ASTRecordLayout& layout = context.getASTRecordLayout(declaration);

            //basic data
            node->type   = declaration->getQualifiedNameAsString();
            node->size   = layout.getSize().getQuantity(); 
            node->align  = layout.getAlignment().getQuantity();

            //Check for bases 

            const clang::CXXRecordDecl* primaryBase = layout.getPrimaryBase();

            if(declaration->isDynamicClass() && !primaryBase && !Helpers::IsMSLayout(context))
            {
                //vtable pointer
                Layout::Node* vPtrNode = new Layout::Node(); 
                vPtrNode->nature = Layout::Category::VTablePtr; 
                vPtrNode->offset = 0u; 
                vPtrNode->size   = context.toCharUnitsFromBits(context.getTargetInfo().getPointerWidth(0)).getQuantity(); 
                vPtrNode->align  = context.toCharUnitsFromBits(context.getTargetInfo().getPointerAlign(0)).getQuantity();
                node->children.push_back(vPtrNode);
            }
            else if(layout.hasOwnVFPtr())
            {
                //vftable pointer
                Layout::Node* vPtrNode = new Layout::Node();
                vPtrNode->nature = Layout::Category::VFTablePtr;
                vPtrNode->offset = 0u;
                vPtrNode->size   = context.toCharUnitsFromBits(context.getTargetInfo().getPointerWidth(0)).getQuantity();
                vPtrNode->align  = context.toCharUnitsFromBits(context.getTargetInfo().getPointerAlign(0)).getQuantity();
                node->children.push_back(vPtrNode);
            }

            //Collect nvbases
            clang::SmallVector<const clang::CXXRecordDecl *,4> bases;
            for(const clang::CXXBaseSpecifier &base : declaration->bases())
            {
                assert(!base.getType()->isDependentType() && "Cannot layout class with dependent bases.");

                if(!base.isVirtual())
                {
                    bases.push_back(base.getType()->getAsCXXRecordDecl());
                }
            }

            // Sort nvbases by offset.
            llvm::stable_sort(bases,[&](const clang::CXXRecordDecl* lhs,const clang::CXXRecordDecl* rhs){ return layout.getBaseClassOffset(lhs) < layout.getBaseClassOffset(rhs); });

            // compute nvbases
            for(const clang::CXXRecordDecl* base : bases)
            {
                Layout::Node* baseNode = ComputeStruct(context,base,false); 
                baseNode->offset = layout.getBaseClassOffset(base).getQuantity();
                baseNode->nature = base == primaryBase? Layout::Category::NVPrimaryBase : Layout::Category::NVBase;
                node->children.push_back(baseNode);
            }

            // vbptr (for Microsoft C++ ABI)
            if(layout.hasOwnVBPtr())
            {                
                //vbtable pointer
                Layout::Node* vPtrNode = new Layout::Node();
                vPtrNode->nature = Layout::Category::VBTablePtr;
                vPtrNode->offset = layout.getVBPtrOffset().getQuantity();
                vPtrNode->size   = context.getTargetInfo().getPointerWidth(0);
                vPtrNode->align  = context.getTargetInfo().getPointerAlign(0);
                node->children.push_back(vPtrNode);
            }

            //Check for fields 
            unsigned int fieldNo = 0;
            for(clang::RecordDecl::field_iterator I = declaration->field_begin(),E = declaration->field_end(); I != E; ++I,++fieldNo)
            {
                const clang::FieldDecl& field = **I;
                const uint64_t localFieldOffsetInBits = layout.getFieldOffset(fieldNo);
                const clang::CharUnits fieldOffset = context.toCharUnitsFromBits(localFieldOffsetInBits);

                // Recursively visit fields of record type.
                if (const clang::CXXRecordDecl* fieldDeclarationCXX = field.getType()->getAsCXXRecordDecl())
                {
                    Layout::Node* fieldNode = ComputeStruct(context,fieldDeclarationCXX,true);
                    fieldNode->name   = field.getNameAsString();
                    fieldNode->type   = field.getType().getAsString(); //check if this or qualified types form function is better
                    fieldNode->offset = fieldOffset.getQuantity();
                    fieldNode->nature = Layout::Category::ComplexField;
                    node->children.push_back(fieldNode);
                }
                else
                {
                    if(field.isBitField())
                    {
                        //field.getType().getAsString() //Field type 
                        //field.getNameAsString(); //field name
                        //uint64_t localFieldByteOffsetInBits = m_context->toBits(fieldOffset - offset);
                        //unsigned Begin = localFieldOffsetInBits - localFieldByteOffsetInBits;
                        //unsigned Width = field.getBitWidthValue(*m_context);

                        //TODO ~ ramonv ~ output bitfield

                        //PrintBitFieldOffset(OS,FieldOffset,Begin,Width,IndentLevel);
                    }
                    else
                    {
                        const clang::TypeInfo fieldInfo = context.getTypeInfo(field.getType());

                        //simple field
                        Layout::Node* fieldNode = new Layout::Node();
                        fieldNode->name   = field.getNameAsString(); 
                        fieldNode->type   = field.getType().getAsString();

                        fieldNode->nature = Layout::Category::SimpleField;
                        fieldNode->offset = fieldOffset.getQuantity();
                        fieldNode->size   = context.toCharUnitsFromBits(fieldInfo.Width).getQuantity();
                        fieldNode->align  = context.toCharUnitsFromBits(fieldInfo.Align).getQuantity();
                        node->children.push_back(fieldNode);
                    }
                }
            }

            //Virtual bases
            if(includeVirtualBases)
            {
                const clang::ASTRecordLayout::VBaseOffsetsMapTy &vtorDisps = layout.getVBaseOffsetsMap();
                for(const clang::CXXBaseSpecifier& Base : declaration->vbases())
                {
                    assert(Base.isVirtual() && "Found non-virtual class!");

                    const clang::CXXRecordDecl* vBase = Base.getType()->getAsCXXRecordDecl();
                    const clang::CharUnits vBaseOffset = layout.getVBaseClassOffset(vBase);

                    if(vtorDisps.find(vBase)->second.hasVtorDisp())
                    {
                        clang::CharUnits size = clang::CharUnits::fromQuantity(4);

                        Layout::Node* vtorDispNode = new Layout::Node();
                        vtorDispNode->nature = Layout::Category::VtorDisp;
                        vtorDispNode->offset = (vBaseOffset - size).getQuantity();
                        vtorDispNode->size   = size.getQuantity();
                        vtorDispNode->align  = size.getQuantity();
                        node->children.push_back(vtorDispNode);
                    }

                    Layout::Node* vBaseNode = ComputeStruct(context,vBase,false);
                    vBaseNode->offset = vBaseOffset.getQuantity();
                    vBaseNode->nature = vBase == primaryBase? Layout::Category::VPrimaryBase : Layout::Category::VBase;
                    node->children.push_back(vBaseNode);
                }
            }

            return node;
        }

    private:
        Layout::Tree bestTree;
        Parser::LocationFilter locationFilter;
    };

    SLayouts g_layouts;

    /////////////////////////////////////////////////////////////////////////////////////////////////////////////

    class FindClassVisitor : public clang::RecursiveASTVisitor<FindClassVisitor> 
    {
    public:
        FindClassVisitor():m_context(nullptr){}

        inline void SetContext(clang::ASTContext* context){ m_context = context; }

        bool VisitCXXRecordDecl(clang::CXXRecordDecl* declaration) 
        {
            //Declaration->dump();

            if ( declaration && ( declaration->isClass() || declaration->isStruct() ) && !declaration->isDependentType() )
            {
                g_layouts.ComputeLayout(*m_context,declaration);
            }

            return true;
        }
    private:
        clang::ASTContext* m_context; 
    };

    class Consumer : public clang::ASTConsumer 
    {
    public:
        virtual void HandleTranslationUnit(clang::ASTContext& context) override
        {
            m_visitor.SetContext(&context);
            m_visitor.TraverseDecl(context.getTranslationUnitDecl());
        }

    private:
        FindClassVisitor m_visitor;
    };

    class Action : public clang::ASTFrontendAction 
    {
    public:
        using ASTConsumerPointer = std::unique_ptr<clang::ASTConsumer>;
        ASTConsumerPointer CreateASTConsumer(clang::CompilerInstance&, llvm::StringRef) override { return std::make_unique<Consumer>(); }
    };
}

namespace 
{
    //group
    llvm::cl::OptionCategory seeCategory("See++ Layout Options");
    llvm::cl::extrahelp SeeCategoryHelp(R"( Exports the struct/class memory layout )");

    //commands
    llvm::cl::opt<std::string> OutputFilename("output", llvm::cl::desc("Specify output filename"), llvm::cl::value_desc("filename"), llvm::cl::cat(seeCategory));

    //aliases
    llvm::cl::alias ShortOutputFilenameOption("o",  llvm::cl::desc("Alias for -output"),  llvm::cl::aliasopt(OutputFilename));
} 

struct ToolFactory : public clang::tooling::FrontendActionFactory 
{
    std::unique_ptr<clang::FrontendAction> create() override { return std::make_unique<ClangParser::Action>(); }
};

namespace Parser
{ 
    void SetFilter(const LocationFilter& filter)
    { 
        ClangParser::g_layouts.SetFilter(filter); 
    }
        
    void ConsoleLog(llvm::StringRef str)
    { 
        IO::ToLogBuffer(str.str().c_str(),str.str().length());
    }

	bool Parse(int argc, const char* argv[])
	{ 
        llvm::outs().SetCustomConsole(&ConsoleLog);
        llvm::outs().SetUnbuffered();
        llvm::errs().SetCustomConsole(&ConsoleLog);
        llvm::errs().SetUnbuffered();

        clang::tooling::CommonOptionsParser optionsParser(argc, argv, seeCategory); 
        clang::tooling::ClangTool tool(optionsParser.getCompilations(), optionsParser.getSourcePathList());
        const int retCode = tool.run(new ToolFactory()); 
        return retCode == 0;
	}

	const Layout::Tree& GetLayout()
	{ 
        return ClangParser::g_layouts.GetLayout();
	}

    void Clear()
    { 
        ClangParser::g_layouts.Clear();
    }
}
#pragma once

#include <vector>
#include <string>

namespace Layout 
{
    enum class Category
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
    };

    // ----------------------------------------------------------------------------------------------------------
    using TAmount = long long;

    // ----------------------------------------------------------------------------------------------------------
    struct Node
    { 
        Node() : nature(Category::Root),offset(0u),size(1u),align(1u){}
        ~Node() { for(Node* child : children) { delete child; } }         //TODO ~ ramonv ~ bad destsruction pattern

        std::string  name;
        std::string  type;
        TAmount      offset;
        TAmount      size;
        TAmount      align;
        Category     nature;

        std::vector<Node*> children;
    };

    // ----------------------------------------------------------------------------------------------------------
    struct Tree
    {        
        Tree()
            : root(nullptr)
            , startLine(0u)
            , startCol(0u)
        {}

        Node* root;
        unsigned int startLine;
        unsigned int startCol; 
    };
}

#pragma once

#include <vector>
#include <string>

namespace Layout 
{
    // ----------------------------------------------------------------------------------------------------------
    using TAmount         = long long;

    // ----------------------------------------------------------------------------------------------------------
    enum class Category : unsigned char
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
    struct Node
    { 
        Node() 
            : nature(Category::Root)
            , offset(0u)
            , size(1u)
            , align(1u)
        {}

        std::string        name;
        std::string        type;
        std::vector<Node*> children;
        TAmount            offset;
        TAmount            size;
        TAmount            align;
        Category           nature;
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

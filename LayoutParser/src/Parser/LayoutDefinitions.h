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
    struct Location
    { 
        Location()
            : line(0u)
            , column(0u)
        {}

        std::string  filename; //TODO ~ ramonv ~ upgrade this to a FileID system
        unsigned int line;
        unsigned int column;
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
        Location           location;
        Category           nature;
    };
}

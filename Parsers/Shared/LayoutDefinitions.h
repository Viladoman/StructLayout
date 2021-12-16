#pragma once

#include <vector>
#include <string>

namespace Layout 
{
    // ----------------------------------------------------------------------------------------------------------
    using TAmount = long long;
    using TFiles  = std::vector<std::string>;

    enum { INVALID_FILE_INDEX = -1 };

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
            : fileIndex(INVALID_FILE_INDEX)
            , line(0u)
            , column(0u)
        {}

        int          fileIndex;
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
        Location           typeLocation;
        Location           fieldLocation;
        Category           nature;
    };

    // ----------------------------------------------------------------------------------------------------------
    struct Result
    { 
        Result()
            : node(nullptr)
        {}

        Node*  node;
        TFiles files; 
    };
}

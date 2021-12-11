#include "IO.h"

#include <vector>
#include <string>

#include "LayoutDefinitions.h"

namespace IO
{ 
    enum { DATA_VERSION = 1 };

    using TBuffer = FILE*;
    using U8 = char;

    namespace Utils
    {
        // -----------------------------------------------------------------------------------------------------------------
        template<typename T> void Binarize(FILE* stream, T input)
        {
            fwrite(&input, sizeof(T), 1, stream);
        }

        // -----------------------------------------------------------------------------------------------------------
        void BinarizeString(FILE* stream, const std::string& str)
        {
            //Perform size encoding in 7bitSize format
            size_t strSize = str.length();
            do
            {
                const U8 val = strSize < 0x80 ? strSize & 0x7F : (strSize & 0x7F) | 0x80;
                fwrite(&val, sizeof(U8), 1, stream);
                strSize >>= 7;
            }
            while (strSize);

            fwrite(str.c_str(), str.length(), 1, stream);
        }

        // -----------------------------------------------------------------------------------------------------------------
        void BinarizeLocation(FILE* stream, const Layout::Location& location)
        { 
            Binarize(stream,location.fileIndex);

            if (location.fileIndex != Layout::INVALID_FILE_INDEX)
            { 
                //valid filename, serialize also line and column
                Binarize(stream,location.line);
                Binarize(stream,location.column);
            }
        }

        // -----------------------------------------------------------------------------------------------------------------
        void BinarizeNode(FILE* stream,const Layout::Node& node)
        {       
            BinarizeString(stream,node.type);
            BinarizeString(stream,node.name);
            Binarize(stream,node.offset);
            Binarize(stream,node.size);
            Binarize(stream,node.align);
            Binarize(stream,node.nature);

            BinarizeLocation(stream,node.typeLocation);
            BinarizeLocation(stream,node.fieldLocation);

            Binarize(stream,static_cast<unsigned int>(node.children.size()));
            for (const Layout::Node* child : node.children)
            { 
                BinarizeNode(stream,*child);
            }  
        }

        // -----------------------------------------------------------------------------------------------------------------
        void BinarizeFiles(FILE* stream, const Layout::TFiles& files)
        {
            Binarize(stream,static_cast<unsigned int>(files.size()));
            for (const std::string& file : files)
            { 
                BinarizeString(stream,file);
            }  
        }
    }

    bool ToFile(const Layout::Result& result, const char* filename)
    {
        FILE* stream;
        const errno_t openResult = fopen_s(&stream, filename, "wb");
        if (openResult)
        {
            return false;
        }

        Utils::Binarize(stream, DATA_VERSION);

        if (result.node)
        {
            Utils::BinarizeFiles(stream, result.files);
            Utils::BinarizeNode(stream, *(result.node));
        }

        fclose(stream);

        return true;
    }

}

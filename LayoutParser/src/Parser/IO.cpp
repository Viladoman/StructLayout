#include <vector>
#include <string>

#include "LayoutDefinitions.h"

namespace IO
{ 
    using TBuffer = std::vector<char>;
    TBuffer g_buffer;

    namespace Utils
    {
        // -----------------------------------------------------------------------------------------------------------------
        template<typename T> void Binarize(TBuffer& buffer, T input)
        {
            buffer.resize(buffer.size()+sizeof(T));
            memcpy(&(*(buffer.end()-sizeof(T))),&input,sizeof(T));
        }

        // -----------------------------------------------------------------------------------------------------------------
        template<> void Binarize<char>(TBuffer& buffer, char input)
        { 
            buffer.push_back(input);
        }

        // -----------------------------------------------------------------------------------------------------------------
        void BinarizeString(TBuffer& buffer, std::string input)
        { 
            const size_t strSize = input.length(); 

            //Perform size encoding in 7bitSize format
            size_t len = strSize;
            do 
            { 
                const char val = len < 0x80? len & 0x7F : (len & 0x7F) | 0x80;
                Binarize(buffer,val);
                len >>= 7;
            }
            while(len);

            if (strSize)
            { 
                //Copy the string
                buffer.resize(buffer.size()+strSize);
                memcpy(&(*(buffer.end()-strSize)),&input[0],strSize);
            }
        }

        // -----------------------------------------------------------------------------------------------------------------
        void BinarizeNode(TBuffer& buffer,const Layout::Node& node)
        {       
            BinarizeString(buffer,node.type);
            BinarizeString(buffer,node.name);
            Binarize(buffer,node.offset);
            Binarize(buffer,node.size);
            Binarize(buffer,node.align);
            Binarize(buffer,static_cast<char>(node.nature));

            Binarize(buffer,static_cast<unsigned int>(node.children.size()));
            for (const Layout::Node* child : node.children)
            { 
                BinarizeNode(buffer,*child);
            }  
        }
    }

    // -----------------------------------------------------------------------------------------------------------------
    void Clear()
    { 
        g_buffer.clear();
    }

    // -----------------------------------------------------------------------------------------------------------------
    bool ToBuffer(const Layout::Tree& tree)
    { 
        Clear();
        if (tree.root)
        {
            Utils::BinarizeNode(g_buffer,*(tree.root));
            return true;
        }
        return false;
    } 

    // -----------------------------------------------------------------------------------------------------------------
    char* GetRawBuffer(unsigned int& size)
    { 
        size = static_cast<unsigned int>(g_buffer.size());
        return g_buffer.empty()? nullptr : &g_buffer[0];
    }
}

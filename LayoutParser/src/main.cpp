#include "Parser/IO.h"
#include "Parser/LayoutDefinitions.h"
#include "Parser/Parser.h"

#define DLLEXPORT __declspec(dllexport)

namespace Utils
{ 
    std::vector<const char*> GenerateFakeCommandLine(const char* input)
    {
        static std::vector<std::string> commandLine; 

        commandLine.clear();
        commandLine.push_back(""); //this fakes the app name

        std::string* current = nullptr; 
        while(*input)
        { 
            const char c = *input;
            if (c == ' ')
            {
                current = nullptr;
            }
            else 
            { 
                if (current == nullptr)
                { 
                    commandLine.emplace_back();
                    current = &commandLine.back();
                }
                current->push_back(*input);
            }
            ++input;
        }

        std::vector<const char*> ret;
        ret.reserve(commandLine.size());
        for (const std::string& str : commandLine)
        { 
            ret.push_back(str.c_str());
        }
        return ret;
    } 

    bool Parse(const char* commandLineArgs)
    { 
        IO::Clear();

        //FILE *fmemopen(void *restrict buf, size_t size, const char *restrict mode);

        //THIS WORKS here - not in VS - VS steals all logs 
        //FILE* stream;
        //freopen_s( &stream, "D:/Code/Clang/freopen.out", "w", stdout );

        std::vector<const char*> args = Utils::GenerateFakeCommandLine(commandLineArgs);
        if (Parser::Parse(static_cast<int>(args.size()),&args[0]))
        { 
            if (IO::ToBuffer(Parser::GetLayout()))
            { 
                return true;
            }
        }

        return false;
    }
}

extern "C"
{
    DLLEXPORT char* GetData(unsigned int* size)
    { 
        return IO::GetRawBuffer(*size);
    }

    DLLEXPORT bool ParseLocation(const char* commandLineArgs, const char* filename, const unsigned int row, const unsigned int col)
    {  
        Parser::SetFilter(Parser::LocationFilter{filename,row,col});
        return Utils::Parse(commandLineArgs);
    }

    /*
    DLLEXPORT bool ParserType(const char* commandLineArgs, const char* typeStr)
    { 
        //TODO ~ ramonv ~ to be implemented
        return Utils::Parse(commandLineArgs);
    }
    */

    DLLEXPORT void Clear()
    {
        Parser::Clear();
        IO::Clear();
    }
}


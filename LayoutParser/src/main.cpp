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

        bool inQuotes = false;

        std::string* current = nullptr; 
        while(*input)
        { 
            const char c = *input;
            if (c == '"')
            { 
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
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

    bool Parse(const char* filename, const char* commandLineArgs)
    { 
        IO::Clear();
        std::vector<const char*> args = Utils::GenerateFakeCommandLine(commandLineArgs);
        if (Parser::Parse(filename, static_cast<int>(args.size()),&args[0]))
        { 
            IO::ToDataBuffer(Parser::GetLayout()); 
            Parser::Clear();
            return true;
        }

        return false;
    }
}

extern "C"
{
    DLLEXPORT char* LayoutParser_GetData(unsigned int* size)
    { 
        return IO::GetDataBuffer(*size);
    }

    DLLEXPORT void LayoutParser_SetLog(IO::TLogFunc logFunc)
    { 
        IO::SetLogFunc(logFunc);
    }

    DLLEXPORT bool LayoutParser_ParseLocation(const char* commandLineArgs, const char* filename, const unsigned int row, const unsigned int col)
    {  
        Parser::SetFilter(Parser::LocationFilter{row,col});
        return Utils::Parse(filename, commandLineArgs);
    }

    DLLEXPORT void LayoutParser_Clear()
    {
        Parser::Clear();
        IO::Clear();
    }
}


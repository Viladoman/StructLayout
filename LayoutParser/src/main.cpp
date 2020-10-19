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
        std::vector<const char*> args = Utils::GenerateFakeCommandLine(commandLineArgs);
        if (Parser::Parse(static_cast<int>(args.size()),&args[0]))
        { 
            //TODO ~ ramonv ~ return error type instead of bool ( ERROR PARSING, FOUND NOTHING, FOUND ) 

            if (IO::ToDataBuffer(Parser::GetLayout()))
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
        return IO::GetDataBuffer(*size);
    }

    DLLEXPORT char* GetLog(unsigned int* size)
    {
        return IO::GetLogBuffer(*size);
    }

    DLLEXPORT bool ParseLocation(const char* commandLineArgs, const char* filename, const unsigned int row, const unsigned int col)
    {  
        Parser::SetFilter(Parser::LocationFilter{filename,row,col});
        return Utils::Parse(commandLineArgs);
    }

    DLLEXPORT void Clear()
    {
        Parser::Clear();
        IO::Clear();
    }
}


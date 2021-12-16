#include "CommandLine.h"

#include "IO.h"

ExportParams::ExportParams()
    : input(nullptr)
    , output(L"tempResult.slbin")
    , locationFile(nullptr)
    , locationLine(0)
{}

namespace CommandLine
{ 
    constexpr int FAILURE = -1;
    constexpr int SUCCESS = 0;

    namespace Utils
    { 
        // -----------------------------------------------------------------------------------------------------------
        int StringCompare(const wchar_t* s1, const wchar_t* s2)
        {
            for(;*s1 && (*s1 == *s2);++s1,++s2){}
            return *(const unsigned char*)s1 - *(const unsigned char*)s2;
        }

        // -----------------------------------------------------------------------------------------------------------
        bool StringToUInt(unsigned int& output, const wchar_t* str)
        { 
            unsigned int ret = 0; 
            while (wchar_t c = *str)
            { 
                if (c < '0' || c > '9') 
                { 
                    return false;
                }

                ret=ret*10+(c-'0'); 
                ++str;
            }

            output = ret;
            return true;
        } 
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////////

    // -----------------------------------------------------------------------------------------------------------
    void DisplayHelp()
    {
        ExportParams defaultParams;
        LOG_ALWAYS("Struct Layout PDB Data Extractor"); 
        LOG_ALWAYS("");
        LOG_ALWAYS("Loads a PDB and tries to extract the type layout."); 
        LOG_ALWAYS("");
        LOG_ALWAYS("Command Legend:"); 
        
        LOG_ALWAYS("-input          (-i)  : The path to the pdb file"); 
        LOG_ALWAYS("-output         (-o)  : The output file path for the results ('%s' by default)",defaultParams.output); 
        LOG_ALWAYS("-locationFile   (-lf) : The source file path where the symbol is located.");
        LOG_ALWAYS("-locationRow    (-lr) : The source file line within the given 'locationFile' where the symbol is located.");
        LOG_ALWAYS("-verbosity      (-v)  : Sets the verbosity level - example: '-v 1'"); 
    }

    // -----------------------------------------------------------------------------------------------------------
    int Parse(ExportParams& params, int argc, wchar_t* argv[])
    { 
        //No args
        if (argc <= 1) 
        {
            LOG_ERROR("No arguments found. Type '?' for help.");
            return FAILURE;
        }

        //Check for Help
        for (int i=1;i<argc;++i)
        { 
            if (Utils::StringCompare(argv[i],L"?") == 0)
            { 
                DisplayHelp();
                return FAILURE;
            }
        }

        //Parse arguments
        for(int i=1;i < argc;++i)
        { 
            wchar_t* argValue = argv[i];
            if (argValue[0] == '-')
            { 
                if ((Utils::StringCompare(argValue,L"-i")==0 || Utils::StringCompare(argValue,L"-input")==0) && (i+1) < argc)
                { 
                    ++i;
                    params.input = argv[i];
                }
                else if ((Utils::StringCompare(argValue,L"-o")==0 || Utils::StringCompare(argValue,L"-output")==0) && (i+1) < argc)
                { 
                    ++i;
                    params.output = argv[i];
                }
                else if ((Utils::StringCompare(argValue, L"-lf") == 0 || Utils::StringCompare(argValue, L"-locationFile") == 0) && (i + 1) < argc)
                {
                    ++i;
                    params.locationFile = argv[i];
                }
                else if ((Utils::StringCompare(argValue, L"-lr") == 0 || Utils::StringCompare(argValue, L"-locationRow") == 0) && (i + 1) < argc)
                {
                    ++i;
                    unsigned int value = 0;
                    if (Utils::StringToUInt(value, argv[i]))
                    {
                        params.locationLine = value;
                    }
                }
                else if ((Utils::StringCompare(argValue,L"-v")==0 || Utils::StringCompare(argValue,L"-verbosity")==0) && (i+1) < argc)
                {
                    ++i;
                    unsigned int value = 0;
                    if (Utils::StringToUInt(value,argv[i]) && value < static_cast<unsigned int>(IO::Verbosity::Invalid))
                    { 
                        IO::SetVerbosityLevel(IO::Verbosity(value));
                    }
                } 
                
            }
            else if (params.input == nullptr)
            { 
                //We assume that the first free argument is the actual input file
                params.input = argValue;
            }
        }

        return 0;
    }
}
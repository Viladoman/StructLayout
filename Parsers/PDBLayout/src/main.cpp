#include "PDBReader.h"

#include "IO.h"

#include "CommandLine.h"

constexpr int FAILURE = -1;
constexpr int SUCCESS = 0;

// -----------------------------------------------------------------------------------------------------------
int wmain(int argc, wchar_t* argv[])
{
    //Parse Command Line arguments
    ExportParams params;
    if (CommandLine::Parse(params, argc, argv) != 0)
    {
        return FAILURE;
    }

    //Execute exporter
    return PDBReader::ExportAtLocation(params.input, params.locationFile, params.locationLine, params.output) ? SUCCESS : FAILURE;
}
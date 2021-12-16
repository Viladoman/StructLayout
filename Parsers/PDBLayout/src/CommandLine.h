#pragma once

struct ExportParams 
{ 
    ExportParams();

    const wchar_t*  input; 
    const wchar_t*  output;
    const wchar_t*  locationFile;
    unsigned int    locationLine; 
};

namespace CommandLine
{ 
    int Parse(ExportParams& args, int argc, wchar_t* argv[]);
}
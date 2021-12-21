#pragma once

#define LOG_ALWAYS(...)   { IO::Log(IO::Verbosity::Always,__VA_ARGS__);               IO::Log(IO::Verbosity::Always,"\n");}
#define LOG_ERROR(...)    { IO::Log(IO::Verbosity::Always,"[ERROR] "##__VA_ARGS__);   IO::Log(IO::Verbosity::Always,"\n");}
#define LOG_WARNING(...)  { IO::Log(IO::Verbosity::Always,"[WARNING] "##__VA_ARGS__); IO::Log(IO::Verbosity::Always,"\n");}
#define LOG_PROGRESS(...) { IO::Log(IO::Verbosity::Progress,__VA_ARGS__);             IO::Log(IO::Verbosity::Progress,"\n");}
#define LOG_INFO(...)     { IO::Log(IO::Verbosity::Info,__VA_ARGS__);                 IO::Log(IO::Verbosity::Info,"\n");}

namespace Layout
{ 
	struct Result;
}

namespace IO
{ 
    //////////////////////////////////////////////////////////////////////////////////////////
    // Logging

    enum class Verbosity
    {
        Always,
        Progress,
        Info,

        Invalid
    };

    void SetVerbosityLevel(const Verbosity level);
    void Log(const Verbosity level, const char* format, ...);
    void LogTime(const Verbosity level, const char* prefix, long miliseconds);

    //////////////////////////////////////////////////////////////////////////////////////////
    // Export

	bool ToFile(const Layout::Result& result, const char* filename);
}
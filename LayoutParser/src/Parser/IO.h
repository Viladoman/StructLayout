#pragma once

namespace Layout
{ 
	struct Result;
}

namespace IO
{ 
	typedef double (__stdcall *TLogFunc)(const char*);

	void Clear();

	bool ToDataBuffer(const Layout::Result& tree);
	char* GetDataBuffer(unsigned int& size);

	void SetLogFunc(TLogFunc func);
    void Log(const char* str);
}
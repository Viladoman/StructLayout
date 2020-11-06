#pragma once

namespace Layout
{ 
	struct Node;
}

namespace IO
{ 
	typedef double (__stdcall *TLogFunc)(const char*);

	void Clear();

	bool ToDataBuffer(const Layout::Node* tree);
	char* GetDataBuffer(unsigned int& size);

	void SetLogFunc(TLogFunc func);
    void Log(const char* str);
}
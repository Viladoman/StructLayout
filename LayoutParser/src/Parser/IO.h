#pragma once

namespace Layout
{ 
	struct Tree;
}

namespace IO
{ 
	void Clear();

	bool ToDataBuffer(const Layout::Tree& tree);
	char* GetDataBuffer(unsigned int& size);

    void ToLogBuffer(const char* str, unsigned int len);
    char* GetLogBuffer(unsigned int& size);
}
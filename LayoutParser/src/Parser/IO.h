#pragma once

namespace Layout
{ 
	struct Tree;
}

namespace IO
{ 
	void Clear();
	bool ToBuffer(const Layout::Tree& tree);
	char* GetRawBuffer(unsigned int& size);
}
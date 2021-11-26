#pragma once

namespace Layout
{ 
	struct Result;
}

namespace IO
{ 
	bool ToFile(const Layout::Result& result, const char* filename);
}
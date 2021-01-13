
namespace Layout
{ 
	struct Result; 
}

namespace Parser
{ 
	struct LocationFilter 
	{ 
		unsigned int row; 
		unsigned int col;
	};

	void SetFilter(const LocationFilter& context);

	bool Parse(const char* filename, int argc, const char* argv[]);
	const Layout::Result& GetLayout();
	void Clear();
}
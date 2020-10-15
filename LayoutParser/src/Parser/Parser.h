
namespace Layout
{ 
	struct Tree; 
}

namespace Parser
{ 
	struct LocationFilter 
	{ 
		const char* filename; 
		unsigned int row; 
		unsigned int col;
	};

	void SetFilter(const LocationFilter& context);

	bool Parse(int argc, const char* argv[]);
	const Layout::Tree& GetLayout();
	void Clear();
}
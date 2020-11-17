#include <cstdio>

struct Empty{};

struct EmptyBase : public Empty
{ 
	float a; 
};

template<typename T>
struct TemplatedStruct
{ 
	T t;
};

struct Virtual
{ 
	virtual void foo(){}

	EmptyBase b;
};

struct VirtualChild : public Virtual
{ 
	TemplatedStruct<double> t; 

	const char* str;

#ifdef TARGET_DEBUG
	double d; 
	double e;
#endif
};


int main()
{ 
	TemplatedStruct<float> temp; 

	printf("HELLO STRUCT LAYOUT!\n");
}
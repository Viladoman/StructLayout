#include <cstdio>
#include <vector>

#include "TestHeader.h"

struct TestStruct : Test::Base
{
    virtual ~TestStruct() {}

    bool  isEnabled;
    void* ptrToData;
    int   amount;

    std::vector<int> collection;
};

int main()
{
    printf("HELLO STRUCT LAYOUT");
}

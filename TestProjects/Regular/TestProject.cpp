#include <cstdio>
#include <vector>
#include <string_view>

#include "TestHeader.h"

struct TestStruct : public Test::Base
{
    virtual ~TestStruct() {}

    bool  isEnabled;
    void* ptrToData;
    int   amount;

    std::string_view sv;
    std::vector<int> collection;
};

struct EmptyStruct{};

int main()
{
    printf("HELLO STRUCT LAYOUT");
}

# StructLayout
Visual Studio Extension for C++ struct memory layout visualization

[Download latest from the Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=RamonViladomat.StructLayout)

## Motivation

In C++, the structure layout can be affected by different factors. In order to produce performant data cache oriented code or reduce the structure memory footprint, it is important to be aware of the class layouts at the same spot where code is created, updated, removed or debugged. This extension allows programmers to visualize their structures within Visual Studio with just 1 click.

## Features

Right click on top of any C++ struct definition and select *Show Struct Layout* (or press **Alt+L**) in order to visualize the memory layout.

![Interaction](https://github.com/Viladoman/StructLayout/wiki/data/StructLayoutTrigger.gif?raw=true)

### Stack Mode

In this visualization mode the types with children are stacked in order to allow navigation in and out and get a better sense of overall structure. 

![Stack screenshot](https://github.com/Viladoman/StructLayout/wiki/data/Stack.png?raw=true)

### Flat Mode

The flat mode removes all parents and only shows up the end types in memory, skipping all groups. This produces a more compact view. 

![Flat screenshot](https://github.com/Viladoman/StructLayout/wiki/data/Flat.png?raw=true)

## How it works

Struct Layout uses Clang LibTooling internally to parse the C++ files and extract the memory layout information.

When a Layout request is made the extension does the following steps: 
+ Retrieve the active document and cursor position. 
+ Extract the relevant file and project properties (cl or nmake)
  1. Indlude directories
  2. Force includes
  3. Preprocessor definitions
  4. Exclude directories
+ Add the extra paramaters from the extension options
+ Trigger the LayoutParser (Clang libtooling application) with all the arguments gathered
+ Visualize the results or print in the Struct Layout Output Pane any issues found. 

## Options & Configurations

Because each solution might need different extra parameters and different needs, the parser options with the extra parameters are stored in a file called **StructLayoutSettings.json** next to the solution. This options can be accessed by pressing the *Options* button in the Struct Layour Tool Window's bottom left corner.

> :warning: if the project is using PCHs, due to the fact that most of the struct definitions are in header files, the precompiled headers might need to be forced included in this configuration settings. 

> :warning: Some big projects use Unity builds or similar. This might lead to ill-formed dependency trees in some files, leading to parsing errors when attempting to parser a single unit.

## Building the VSIX 

Struct Layout uses llvm and clang libtooling to parse the C++ files and extract the requested memory layouts. This means that the C# VSIX extensions uses an unmanaged DLL with the libtooling that we will need to compile first. 

### Generate the LayoutParser.dll

#### Download llvm-project
First step would be to get the llvm-project with clang. 
There is more detailed information on how to set it up at the [Getting Started with Clang](https://clang.llvm.org/get_started.html) page.

**Important:** Because Visual Studio operates on 32bits, it is important to generate the llvm projects for 32bits or the dll won't be compatible.

if the llvm-project folder is located in the repository root folder, the LayoutParser project should pick up all include directories and libraries correctly. 

#### Modify 
In order to be able to retrieve the stdout/stderr from llvm, I little modication has been made to llvm::raw_fd_ostream. 
The modifications are the following:

**raw_ostream.h:**
```
line 431:
  typedef void (*TCustomConsole)(StringRef);
  TCustomConsole customConsole = nullptr;
line 496: 
  void SetCustomConsole(TCustomConsole console){ customConsole = console; }
```

**raw_ostream.cpp:**
```
line 742:
  if (customConsole)
  {
      customConsole(StringRef(Ptr, Size));
      return;
  }
```

Local copies for reference can be found at the *ClangMods* folder.

#### Build LayoutParser.dll
Open the LayoutParser solution found at *LayoutParser/LayoutParser.sln* and build on *x86|Release*. This will already copy the generated dll to the StructLayout VSIX folder. 

### Generate the VSIX 
Open and build the solution found at *StructLayout/StructLayout.sln*.
There is also a sample project for testing at *TestProject/TestProject.sln*.

## References
- [Visual Studio 2019](https://visualstudio.microsoft.com/vs/)
- [LLVM](http://llvm.org/)

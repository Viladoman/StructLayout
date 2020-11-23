# StructLayout

Visual Studio Extension for C++ struct memory layout visualization

[![MarketPlace](https://img.shields.io/badge/Visual_Studio_Marketplace-Latest-green.svg)](https://marketplace.visualstudio.com/items?itemName=RamonViladomat.StructLayout)
[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/cgi-bin/webscr?cmd=_donations&business=T2ZVTJM6S7926)

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

The flat mode skips all groups and only shows one layer, producing a more compact view. 

![Flat screenshot](https://github.com/Viladoman/StructLayout/wiki/data/Flat.png?raw=true)

## How it works

Struct Layout uses Clang LibTooling internally to parse the C++ files and extract the memory layout information.

When a Layout request is made the extension does the following: 
+ Retrieve the active document and cursor position. 
+ Extract the relevant file and project properties (cl or nmake).
  1. Include directories
  2. Force includes
  3. Preprocessor definitions
  4. Exclude directories
+ Add the extra parameters from the extension options.
+ Trigger the LayoutParser (Clang libtooling application) with all the arguments gathered.
+ Visualize the results or print any issues found in the *StructLayout Output Pane*. 

## Options & Configurations

[More detailed information in the Configurations Page](https://github.com/Viladoman/StructLayout/wiki/Configurations)

Because each solution might need different extra parameters and different needs, the parser options with the extra parameters are stored in a file called **StructLayoutSettings.json** next to the solution. This options can be accessed by pressing the *Options* button in the Extensions tab or in the Struct Layout Tool Window's bottom left corner.

The additional include directories, force includes and preprocessor definitions can be typed in following the same format as the Visual Studio properties (*;* separated).
Example:
```
includePathA;${SolutionDir}Folder/;${ProjectDir}...
```

The extra arguments will be appended as they are after a Macro replacement pass.
Example:
```
-std=c++17 -ftime-trace -I${SolutionDir}Folder/
```

> :warning: If the project is using PCHs, due to the fact that most of the struct definitions are in header files, the precompiled headers might need to be forced included. 

> :warning: Some big projects use Unity builds or similar. This might lead to ill-formed dependency trees in some files, leading to parsing errors when attempting to parse a single unit.

#### Special Configurations
[Unreal Engine 4 Configuration](https://github.com/Viladoman/StructLayout/wiki/Configurations#unreal-engine-4)

## References
- [Visual Studio 2019](https://visualstudio.microsoft.com/vs/)
- [LLVM](http://llvm.org/)

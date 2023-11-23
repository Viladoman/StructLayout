# StructLayout

Visual Studio Extension for C++ struct memory layout visualization

[![MarketPlace2022](https://img.shields.io/badge/Visual_Studio_Marketplace-VS2022-green.svg)](https://marketplace.visualstudio.com/items?itemName=RamonViladomat.StructLayout2022)
[![MarketPlace](https://img.shields.io/badge/Visual_Studio_Marketplace-VS2019-green.svg)](https://marketplace.visualstudio.com/items?itemName=RamonViladomat.StructLayout)
[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/donate?hosted_button_id=QWTUS8PNK5X5A)

[Download latest from the Visual Studio Marketplace 2022](https://marketplace.visualstudio.com/items?itemName=RamonViladomat.StructLayout2022)

[Download latest from the Visual Studio Marketplace 2019](https://marketplace.visualstudio.com/items?itemName=RamonViladomat.StructLayout)

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

Struct Layout can use different systems to parse the C++ files and extract the memory layout information. Depending on the complexity and quirks of the build system and Visual Studio setup one option will be more convinent than the others. The method used can be changed in the [Extension Options Window](https://github.com/Viladoman/StructLayout/wiki/Configurations)

When a Layout request is made the extension does the following: 
+ Retrieve the active document and cursor position. 
+ Extract the relevant project context.
+ Add/Override any extra parameters from the extension options.
+ Trigger the selected LayoutParser with all the arguments gathered.
+ Visualize the results or print any issues found in the *StructLayout Output Pane*. 

### Clang Libtooling

This method will process the file location through a Clang LibTooling executable which will parse the current file and headers. This method can give really accurate results as it retrieves the data directly from the Clang AST but it will need the exact build context to be able to properly understand all the code.

When a query to the clang libtooling is triggered the extension will try to gather the following data from the active project and configuration: 
1. Include directories
2. Force includes
3. Preprocessor definitions
4. Exclude directories

### PDB 

This method takes advantage of the fact that the pdb (Program DataBase) will most likely contain all the layout information for all user defined types. This application uses the DIA SDK (Debug Interface Access) to open and query the pdb. This system can be useful if our setup is not ready to be compiled with a Clang compiler, the build system is quite complex hitting some corner cases or we have some MSVC specific code. The caveat is that we would need to compile the projects before performing any queries keeping the pdbs up to date. 

## Documentation
- [Configurations and Options](https://github.com/Viladoman/StructLayout/wiki/Configurations)
- [Using Unreal Engine](https://github.com/Viladoman/StructLayout/wiki/Unreal-Engine-Configuration)
- [Building the VSIX](https://github.com/Viladoman/StructLayout/wiki/Building-the-VSIX)

## References
- [Visual Studio](https://visualstudio.microsoft.com/vs/)
- [LLVM](http://llvm.org/)
- [DIA SDK](https://docs.microsoft.com/en-us/visualstudio/debugger/debug-interface-access/debug-interface-access-sdk)

## Contributing
This project is open to code contributions. 

If you found this extension useful you can always buy me a cup coffee. 

[![paypal](https://www.paypalobjects.com/en_US/i/btn/btn_donate_SM.gif)](https://www.paypal.com/donate?hosted_button_id=QWTUS8PNK5X5A)

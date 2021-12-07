# StructLayout

Visual Studio Extension for C++ struct memory layout visualization

[![MarketPlace](https://img.shields.io/badge/Visual_Studio_Marketplace-VS2019-green.svg)](https://marketplace.visualstudio.com/items?itemName=RamonViladomat.StructLayout)
[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/donate?hosted_button_id=QWTUS8PNK5X5A)

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

## Documentation
- [Configurations and Options](https://github.com/Viladoman/StructLayout/wiki/Configurations)
- [Working Unreal Engine 4](https://github.com/Viladoman/StructLayout/wiki/Unreal-Engine-4-Configuration)
- [Building the VSIX](https://github.com/Viladoman/StructLayout/wiki/Building-the-VSIX)

## References
- [Visual Studio 2019](https://visualstudio.microsoft.com/vs/)
- [LLVM](http://llvm.org/)

## Contributing
This project is open to code contributions. 

If you found this extension useful you can always buy me a cup coffee. 

[![paypal](https://www.paypalobjects.com/en_US/i/btn/btn_donate_SM.gif)](https://www.paypal.com/donate?hosted_button_id=QWTUS8PNK5X5A)

# Binding Tools for Swift Functional Outline
There are a number of functional components to Binding Tools for Swift and generally speaking, they are separate pieces. This document will describe each piece and how they fit together. Before going into deep details, I will talk about the biggest components and how and why they fit together.


## Steps To Creating a Swift Binding
1. Analyze output library (Inventory) and map entry points to mangled names (Demangling)
2. Reflect on output library and aggregate the public API (reflector, SwiftXmlReflection)
3. Write swift wrappers for all entry points that can’t be called directly from C# (MethodWrapping, OverrideBuilder)
4. Compile the wrappers to a helper library (WrappingCompiler)
5. Analyze the output wrapping library (inventory) and map entry points to mangled names
6. Reflect on output wrapping library and aggregate the public API
7. Write C# bindings that use the entry points from steps 5 and 3 (NewClassCompiler)

The flow generally goes like this:

1. source library → Demangler → Inventory
2. source .swiftmodule/library → Reflector → ModuleDeclaration(s)
3. Inventory/ModuleDeclarations → WrappingCompiler → wrapper source
4. Inventory/ModuleDeclarations → OverrideBuilder → wrapper source
5. wrapper source → WrappingCompiler → wrapper library
6. wrapper library → Demangler → wrapper Inventory
7. wrapper .swiftmodule/library → Reflector → wrapper ModuleDeclaration(s)
8. (ModuleDeclarations, Inventory, wrapper ModuleDeclarations, wrapper Inventory) → NewClassCompiler → C# binding

Along the way, there are several things that happen as side effects of the overall. First, there is a type database which contains swift types and information associated with them:

- The kind of the type (class, struct, enum, etc)
- The name of the type including the module
- Whether or not it’s ObjC or swift
- The C# namespace and full type name

This database is built from either reading XML files or in the process reflecting on module declarations and writing the C# bindings.

Second there three compile-time marshaling engines that handle transitions from:

1. C# → swift
2. swift → “C safe” C#
3. C safe C# → C#


# Major Components And Their Use (In No Particular Order)
## Dynamo

Dynamo is a code generator that uses C# combinators to write source code. Dynamo is a somewhat leaky abstraction that models language structures similar to a compiler parse tree and handles writing out the code for you. This was meant as a step up from using `Console.WriteLine`. There are sub modules within Dynamo for writing swift source code and writing C# source code. Combinators let you use C# expressions to build up structures. For example, there is an abstract type `SLBaseExpr` for which many operators exist. The C# expression `someBaseExpr + someOtherBaseExpr` generates an `SLBinaryExpr` with a `+` operator.

Generally speaking, the types are immutable except where they are or contain collections, which are mutable. In this way, you can easily, say, make a method and add to its contents or parameters, but not modify its visibility, return type, etc.

Dynamo is used in wrapping and binding.


## SwiftType Hierarchy

This is one of the oldest components in the project. I had hoped initially that I could use it to represent all of the swift data types and entry points. This turned out to be not true. Still, each type in the hierarchy represents all the majors types in swift.
The types in this hierarchy are meant to be immutable.


## Demangler

 The entry points in a swift library are mangled ASCII strings. The swift name mangling scheme has changed several times since swift 3.0. In swift 3.0, it was a small prefix based language. In swift 4.0, it changed to a postfix based language. swift 5.0 is similar to swift 4.0 with some changes. All of the demanglers generates SwiftType objects and bind them into a `TLDefinition`. A `TLDefinition` may be a function or global data etc.
 

##  Inventory

 The inventory hierarchy is a set of pairs of classes. An `FooInventory` contains zero or more `FooContents`. A `FooContents` is all the elements that make up a `Foo`.  For example, a `ModuleInventory` contains one or more `ModuleContents`. A `ModuleContents` contains a `ClassInventory`, a `FunctionInventory`, a `VariableInventory` and so on.
 
 In general the inventories operate like a dynamic pachinko machine. If you drop a `TLDefinition` which represents a method on a class onto the top `ModuleInventory`, it will dispatch it to a `ModuleContents` which will in turn find or make the appropriate `ClassInventory` which will find or create the appropriate `ClassContents` and drop it onto this, which will in turn put it into a `FunctionInventory`, which will then drop it into `FunctionContents` which will in turn go into an `OverloadInventory` and finally into an `OverloadContents`.
 
 After all the public `TLDefinition` objects are dispensed, there is a more or less complete representation of all the types and their public entry points in the inventory. Unfortunately, this was not complete because there is a pile of missing information, including attributes, struct layout, etc. The inventory still proved to be useful, however, so it wasn’t scrapped.
 

##  Importing

 In order to interoperate with ObjC bindings generated for Xamarin.iOS/mac/tvos/watchos, we need a way to map swift types to existing C# bindings. In order to do this, we rip through all the C# types and build up type database entries from them.
 

##  Reflector

The reflector is a modified version of the swift compiler that consumes swift libraries and writes out an XML representation of the public facing API. This is necessary in order to capture all the details and information associated with an API. The swift compiler provides a visitor pattern that allows virtual methods to get called for each of the nodes in the parse tree of the module. Based on this we can output XML depending on the node that we’re in.


## SwiftXmlReflection

This hierarchy is very similar to `SwiftType`, but has methods/properties to represent information that isn’t present in the mangled signatures. In addition, there is another type thrown in called `TypeSpec`. When swift generates type information for a given return value or parameter type, it uses a little language to represent the type. There are roughly 4 types that are represented in this: named types, tuples, closures, and protocol lists. The little language encodes all of this. `TypeSpecParser` is a simple recursive descent parser that consumes the little language and generates one of the `TypeSpec` types representing it.


## TopLevelFunctionCompiler

TopLevelFunctionCompiler is a set of tools that given a `FunctionDeclaration` can generate a C# method signature, property signature, or a delegate declaration. This used by `NewClassCompiler` to generate public facing API, virtual callbacks, or pinvoke definitions.


## MethodWrapping

Given a public swift API (function, class, struct, extension, etc), MethodWrapping generates swift code that can be called from a pinvoke in C#


## OverrideBuilder

Given an `open` swift class or a protocol, `OverrideBuilder` generates either an override of the type, overriding all the virtual types and delegating them to a vtable into C#, or it generates a set of extensions on the type `EveryProtocol` implementing all the types in the protocol and delegating them to a vtable into C#.


## TypeMapping

The TypeMapping namespace contains classes to map types from one type to another as well as maintaining the `TypeDatabase`. `TypeMapper` maps from swift types to C# types. `SwiftTypeToSLType` maps from the `SwiftType` hierarchy to the Dynamo `SLType` hierarchy. There is a similar one to map from `TypeSpec` objects to the `SLType` hierarchy. The latter two get bundled into the `TypeMapper` object so that all three are easily accessible.


## CustomSwiftCompiler

This is a chunk of code that gets used to compile swift source code into libraries and swift module files. Given source files and dependencies, it figures out how to tell the compiler about all the necessary references to compile the source files correctly. It also handles multiple target platforms and merging the output into a far framework.


## NewClassCompiler

`NewClassCompiler` handles generating the C# bindings onto the types from the swift module. It orchestrates the other components into writing wrappers and then finally writing the actual C# bindings. It is a huge file. Yes, I know this. More than anything else, this reflects the complexity of representing swift in C# as well the all the special cases involved in marshaling. If you’re looking to trace through it, a good place to start is `CompileModuleContents` at the highest level. If you’re looking to catch a particular type, there are methods named `Compile{Classes,Structs,Enums,Extensions,etc.}` which do what they say on the box. Classes handle virtual and non-virtual cases separately.


## Unit Tests

Binding Tools for Swift is heavily unit tested. The general pattern that is used is a test contains a string which is swift code. It also contains some Dynamo combinators to build the code that will use the Binding Tools for Swift binding. The call `TestRunning.TestAndExecute` compiled the swift code into a library then runs Binding Tools for Swift on it to generate wrapping and binding. The Dynamo combinators get written into a C# file and then the whole thing gets compiled and run using mono. The output gets collected and is compared to the assert.
In addition, if there is no platform specified, the swift source code and the Dynamo code will get aggregated into code appropriate for running on a device or simulator.

Notes:

- In any given test namespace, tests swift source should have unique names for classes, functions, etc. Name conflicts will fail the device test builds
- Avoid generating output from swift using `print`. It’s a real pain to capture and redirect the output to C# from a device. There are very small number of tests that do this and they feel flimsy as a result. In addition, swift IO routines buffer the output such that if you print something in swift and then in C#, you will see the C# output first. Prefer tests that return a value to C# and let C# print the result.

 




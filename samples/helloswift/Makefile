SWIFT_BIN = ../../bin/swift/bin

SWIFTC = $(SWIFT_BIN)/swiftc
SWIFTARGS = -sdk `xcrun --show-sdk-path` -emit-module -emit-library

BINDINGTOOLSFORSWIFT=../../binding-tools-for-swift

OUTPUT_MODULE=HelloMod

libHello.dylib: *.swift *.cs
	@cp ../../lib/SwiftInterop/SwiftRuntimeLibrary.Mac.dll .
	@cp ../../lib/SwiftInterop/mac/XamGlue.framework/XamGlue .
	$(SWIFTC) $(SWIFTARGS) -module-name $(OUTPUT_MODULE) *.swift
	$(BINDINGTOOLSFORSWIFT) --retain-swift-wrappers -o . -C . $(OUTPUT_MODULE) 
	mcs -nowarn:CS0169 -lib:../lib -r:SwiftRuntimeLibrary.Mac -lib:. *.cs -out:$(OUTPUT_MODULE).exe

runit:
	@LD_LIBRARY_PATH=.:$(SWIFT_GLUE):$(SWIFT_LIB) mono --arch=64 $(OUTPUT_MODULE).exe

clean:
	@rm -f *.dylib *.swiftmodule *.swiftdoc *.dll *.exe XamGlue HelloHelloMod.cs
	@rm -rf XamWrappingSource x86_64 XmlReflection

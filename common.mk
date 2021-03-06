# 'all' is the default target, and must always come first
all:

$(TOP)/stconfig.inc: $(TOP)/stconfig.sh
	@$(TOP)/stconfig.sh
-include $(TOP)/stconfig.inc

# This will never point to the swiftc compiler in our packaged directory (where SWIFTC may point to if SOM_PATH is set)
# We need this if we need swiftc before the package is created (because that's where SOM_PATH points to)
UNPACKAGED_SWIFTC=$(abspath $(TOP)/apple/build/Ninja-ReleaseAssert/swift-macosx-x86_64/bin/swiftc)
UNPACKAGED_SWIFTLIB=$(abspath $(TOP)/apple/build/Ninja-ReleaseAssert/swift-macosx-x86_64/lib/swift)

PLIST_SWIFTY:=mono --debug $(abspath $(TOP)/plist-swifty/bin/Debug/plist-swifty.exe)
TYPE_O_MATIC:=mono --debug $(abspath $(TOP)/type-o-matic/bin/Debug/type-o-matic.exe)
TOM_SWIFTY:=mono --debug $(abspath $(TOP)/tom-swifty/bin/Debug/tom-swifty.exe)

# Clone files instead of copying them on APFS file systems. Much faster.
CP:=$(shell df -t apfs / >/dev/null 2>&1 && echo "cp -c" || echo "cp")

# quiet commands
Q            = $(if $(V),,@)
Q_2          = $(if $(V),,@echo "$(1) $(2) $(@F)";)
Q_FRAMEWORK  = $(if $(V),,@echo "GEN    $(NAME).framework";)
Q_GEN        = $(if $(V),,@echo "GEN    $(@F)";)
Q_LIPO       = $(if $(V),,@echo "LIPO   $(@F)";)
Q_SOM        = $(if $(V),,@echo "BINDINGTOOLSFORSWIFT $(NAME)";)
Q_XCODEBUILD = $(if $(V),,@echo "XCODEBUILD/$* $(NAME)";)
Q_SWIFTC     = $(if $(V),,@echo "SWIFTC $(@F)";)

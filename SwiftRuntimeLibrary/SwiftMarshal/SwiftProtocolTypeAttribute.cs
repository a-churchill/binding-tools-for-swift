// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Reflection;
using System.Runtime.InteropServices;
#if __IOS__
using ObjCRuntime;
#endif

namespace SwiftRuntimeLibrary.SwiftMarshal {
	[AttributeUsage (AttributeTargets.Interface, AllowMultiple = false)]
	public sealed class SwiftProtocolTypeAttribute : Attribute {
		public SwiftProtocolTypeAttribute (Type proxyType, string libraryName, string protocolDescriptor, bool isAssociatedTypeProtocol = false)
		{
			if (proxyType == null)
				throw new ArgumentNullException (nameof (proxyType));
			ProxyType = proxyType;
			LibraryName = Exceptions.ThrowOnNull (libraryName, nameof (libraryName));
			ProtocolDescriptor = Exceptions.ThrowOnNull (protocolDescriptor, nameof (protocolDescriptor));
			IsAssociatedTypeProtocol = isAssociatedTypeProtocol;
		}
		public Type ProxyType { get; private set; }
		public string LibraryName { get; private set; }
		public string ProtocolDescriptor { get; private set; }
		public bool IsAssociatedTypeProtocol { get; private set; }

#if __IOS__
		[MonoNativeFunctionWrapper]
#endif
		delegate IntPtr MetatypeFunctionType ();

		public bool IsAssociatedTypeProxy ()
		{
			return IsAssociatedTypeProtocol;
		}

		public static bool IsAssociatedTypeProxy (Type type)
		{
			var proxyAttr = type.GetCustomAttribute<SwiftProtocolTypeAttribute> ();
			return proxyAttr != null ? proxyAttr.IsAssociatedTypeProxy () : false;
		}

		public static Type ProxyTypeForInterfaceType (Type interfaceType)
		{
			var proxyAttr = interfaceType.GetCustomAttribute<SwiftProtocolTypeAttribute> ();
			if (proxyAttr == null)
				throw new SwiftRuntimeException ($"Type {interfaceType.Name} does not have a SwiftProtocol attribute.");
			return proxyAttr.ProxyType;
		}

		public static BaseProxy MakeProxy (Type interfaceType, object interfaceImpl, EveryProtocol protocol)
		{
			var proxyType = ProxyTypeForInterfaceType (interfaceType);

			var ci = proxyType.GetConstructor (new Type [] { interfaceType, typeof (EveryProtocol) });
			if (ci == null)
				throw new SwiftRuntimeException ($"Type {proxyType.Name} does not have a constructor that takes {interfaceType.Name} and {typeof (EveryProtocol).Name}");

			return (BaseProxy)ci.Invoke (new object [] { interfaceImpl, protocol });
		}

		public static BaseProxy MakeProxy (Type interfaceType, ISwiftExistentialContainer container)
		{
			var proxyType = ProxyTypeForInterfaceType (interfaceType);

			var ci = proxyType.GetConstructor (new Type [] { typeof (ISwiftExistentialContainer) });
			if (ci == null)
				throw new SwiftRuntimeException ($"Type {proxyType.Name} does not have a constructor that an ISwiftExistentialContainer");

			return (BaseProxy)ci.Invoke (new object [] { container });
		}

		public static SwiftNominalTypeDescriptor DescriptorForType (Type interfaceType)
		{
			Exceptions.ThrowOnNull (interfaceType, nameof (interfaceType));
			if (!interfaceType.IsInterface)
				throw new SwiftRuntimeException ($"Type {interfaceType.Name} is not an interface.");
			var typeAttribute = interfaceType.GetCustomAttribute<SwiftProtocolTypeAttribute> ();
			if (typeAttribute == null)
				throw new SwiftRuntimeException ($"Type {interfaceType.Name} does not have a SwiftProtocolType attribute");
			var desc = SwiftNominalTypeDescriptor.FromDylibFile (typeAttribute.LibraryName, DLOpenMode.Now, typeAttribute.ProtocolDescriptor);
			if (!desc.HasValue)
				throw new SwiftRuntimeException ($"Unable to find swift protocol type descriptor for {interfaceType.Name} with symbol {typeAttribute.ProtocolDescriptor} in file {typeAttribute.LibraryName}");
			return desc.Value;
		}

		internal static IntPtr DescriptorHandleForType (Type interfaceType)
		{
			// internal version, no exceptions
			var typeAttribute = interfaceType.GetCustomAttribute<SwiftProtocolTypeAttribute> ();
			if (typeAttribute == null)
				return IntPtr.Zero;
			var desc = SwiftNominalTypeDescriptor.FromDylibFile (typeAttribute.LibraryName, DLOpenMode.Now, typeAttribute.ProtocolDescriptor);
			if (!desc.HasValue)
				return IntPtr.Zero;
			return desc.Value.Handle;
		}
	}
}

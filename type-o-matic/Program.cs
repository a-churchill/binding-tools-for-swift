// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SwiftReflector;
using SwiftReflector.Importing;
using Mono.Cecil;
using Dynamo;
using Dynamo.SwiftLang;
using Dynamo.CSLang;
using Xamarin;
using SwiftReflector.Demangling;
using System.Text;

namespace typeomatic {
	class MainClass {
		static string kRobotText = "This file is generated by a robot. Do not edit. Beep boop.";
		public static int Main (string [] args)
		{
			TypeOMaticOptions options = new TypeOMaticOptions ();
			var extra = options.ParseCommandLine (args);

			if (options.PrintHelp) {
				options.PrintUsage (Console.Out);
				return 0;
			}

			if (extra.Count > 0) {
				// Warn about extra params that are ignored.
				Console.WriteLine ($"WARNING: The following extra parameters will be ignored: '{ String.Join (",", extra) }'");
			}

			if (string.IsNullOrEmpty (options.SwiftLibPath)) {
				Console.WriteLine ("Unable to find the custom swift compiler libraries. Try using --swift-lib-path.");
				return 1;
			}

	    		if (options.Platform == PlatformName.None) {
				Console.WriteLine ($"Unknown platform {options.Platform}");
				options.PrintUsage (Console.Out);
				return 1;
			}

			var targetDirectory = Path.Combine (options.SwiftLibPath, PlaformToDirectoryName (options.Platform));
			if (!Directory.Exists (targetDirectory)) {
				Console.WriteLine ($"Unable to find library directory {targetDirectory}");
				return 1;
			}

	    		if ((options.Framework != null && options.Namespaces.Count > 0) ||
	    			(options.Framework == null && options.Namespaces.Count == 0)) {
				Console.WriteLine ("You need to select either input namespaces (to generate swift) or a XamGlue.framework (to generate C#) but not both.");
				return 1;
			}
			
			if (options.Framework != null && !Directory.Exists (options.Framework)) {
				Console.WriteLine ($"Unable to find XamGlue framework at {options.Framework}.");
				return 1;
			}

			var aggregatedTypes = new TypeAggregator (options.Platform);
			aggregatedTypes.Aggregate ();

			var writer = new CodeWriter (options.OutputWriter);
			if (options.Namespaces.Count > 0) {
				var slFile = GenerateStubsFromTypes (aggregatedTypes, options.Platform, options.Namespaces);
				slFile.WriteAll (writer);
			} else {
				var csFile = GeneratePInvokesFromTypes (aggregatedTypes, options.Platform, options.Framework);
				csFile.WriteAll (writer);
			}
			writer.TextWriter.Flush ();
			if (options.OutputWriter != Console.Out) {
				options.OutputWriter.Close ();
				options.OutputWriter.Dispose ();
			}

			return 0;
		}


		static CSFile GeneratePInvokesFromTypes (TypeAggregator types, PlatformName platform, string framework)
		{
			var fileName = Path.GetFileNameWithoutExtension (framework); // /path/XamGlue.framework -> XamGlue
			var dylibFile = Path.Combine (framework, fileName);
			var funcs = TLFunctionsForFile (dylibFile, platform);

			var ns = new CSNamespace ("SwiftRuntimeLibrary.SwiftMarshal");
			var use = new CSUsingPackages ();
			use.And (new CSUsing ("System.Runtime.InteropServices"))
				.And (new CSUsing ("System"))
				.And (new CSUsing ("System.Collections.Generic"))
				.And (new CSUsing ("SwiftRuntimeLibrary"));
		
			var csFile = new CSFile (use, new CSNamespace [] { ns });
			var csClass = new CSClass (CSVisibility.Internal, $"{fileName}Metadata");
			new CSComment (kRobotText).AttachBefore (use);

			CSConditionalCompilation.If (PlatformToCSCondition (platform)).AttachBefore (use);
			CSConditionalCompilation.Endif.AttachAfter (ns);
			ns.Block.Add (csClass);

			var typeOntoPinvoke = new List<KeyValuePair<CSBaseExpression, CSBaseExpression>> ();

			var typesToProcess = new List<TypeDefinition> ();
			typesToProcess.AddRange (types.PublicEnums);
			typesToProcess.AddRange (types.PublicStructs);

			// pre-sort by function name
			typesToProcess.Sort ((type1, type2) => String.CompareOrdinal (FuncIDForTypeDefinition (type1), FuncIDForTypeDefinition (type2)));

			foreach (var type in typesToProcess) {
				if (type.HasGenericParameters)
					continue;
				var moduleName = type.Namespace;
				var name = type.Name;
				if (TypeAggregator.FilterModuleAndName (platform, moduleName, ref name)) {
					var pinvoke = PInvokeForType (type, funcs);
					if (pinvoke != null) {
						csClass.Methods.Add (pinvoke);
						use.AddIfNotPresent (type.Namespace);
						var typeOf = new CSSimpleType (type.FullName).Typeof ();
						var funcName = pinvoke.Name;
						typeOntoPinvoke.Add (new KeyValuePair<CSBaseExpression, CSBaseExpression> (typeOf, funcName));
					}
				}
			}
			
			var initializers = typeOntoPinvoke.Select (typeAndFunc => new CSInitializer (new CSBaseExpression [] { typeAndFunc.Key, typeAndFunc.Value }, false));
			var bindingExpr = new CSInitializedType (new CSFunctionCall ("Dictionary<Type, Func<SwiftMetatype>>", true), new CSInitializer (initializers, true));
			var bindingDecl = new CSFieldDeclaration (new CSSimpleType ("Dictionary<Type, Func<SwiftMetatype>>"), "ObjCBindingSwiftMetatypes", bindingExpr, CSVisibility.Internal, true);
			csClass.Fields.Add (new CSLine (bindingDecl));

			use.Sort ((package1, package2) => String.CompareOrdinal (package1.Package, package2.Package));

			return csFile;
		}

		static CSMethod PInvokeForType (TypeDefinition type, Dictionary<string, string> funcs)
		{
			var funcID = FuncIDForTypeDefinition (type);
			string mangledName;
			if (!funcs.TryGetValue (funcID, out mangledName))
				return null;

	    		return CSMethod.PInvoke (CSVisibility.Internal, new CSSimpleType ("SwiftMetatype"), funcID, new CSIdentifier ("SwiftCore.kXamGlue"),
					mangledName.Substring (1), new CSParameterList ());
		}


		static SLFunc MetadataFuncForType (PlatformName platform, TypeDefinition type, TypeType entityType)
		{
			var name = type.Name;
			var moduleName = type.Namespace;
			// typename based on C# type name, not remapped name
			var funcID = new SLIdentifier (FuncIDForTypeDefinition (type));
			TypeAggregator.RemapModuleAndName (platform, ref moduleName, ref name, entityType);
			var value = new SLIdentifier ($"{name}.self");
			var returnLine = SLReturn.ReturnLine (value);
			var body = new SLCodeBlock (null);
			body.Add (returnLine);
			var func = new SLFunc (Visibility.Public, new SLSimpleType ("Any.Type"), funcID, null, body);
			new SLComment (type.FullName, true).AttachBefore (func);
			var attr = AvailableAttributeForFunc (platform, type);
			if (attr != null)
				attr.AttachBefore (func);
			return func;
		}

		static string FuncIDForTypeDefinition (TypeDefinition type)
		{
			// Used to be MetaDataWrapperFor
	    		// The size of this symbol gets magnified by the number of types.
			// Removing 14 characters saves ~32K
			return $"MDW_{type.Namespace}_{type.Name}";
		}

		// for debugging
		static void WriteAllNamespaces (TypeAggregator types)
		{
			foreach (var type in types.AllTypes) {
				Console.WriteLine (type.Namespace);
			}
		}

		static SLFile GenerateStubsFromTypes (TypeAggregator types, PlatformName platform, List<string> namespaces)
		{
			var imports = ImportsForPlatform (platform, types.AllTypes);
			SLConditionalCompilation.If (PlatformCondition (platform)).AttachBefore (imports);
			new SLComment (kRobotText, true).AttachBefore (imports);
			var slfile = new SLFile (imports);

			slfile.Functions.AddRange (MetaWrapperForFunc (platform, types.PublicEnums, TypeType.Enum, namespaces));
			slfile.Functions.AddRange (MetaWrapperForFunc (platform, types.PublicStructs, TypeType.Struct, namespaces));

			// can't do ordinal because function names can have Bockovers
			slfile.Functions.Sort ((func1, func2) => String.Compare (func1.Name.Name, func2.Name.Name, StringComparison.InvariantCulture));

			slfile.Trailer.Add (SLConditionalCompilation.Endif);
			// can't do ordinal because module names can have Bockovers
			imports.Sort ((import1, import2) => String.Compare (import1.Module, import2.Module, StringComparison.InvariantCulture));
			return slfile;
		}

		static IEnumerable <SLFunc> MetaWrapperForFunc (PlatformName platform, IEnumerable<TypeDefinition> types,
								TypeType entityType, List<string> namespaces)
		{
			foreach (var type in types) {
				if (namespaces.Count > 0 && !namespaces.Contains (type.Namespace))
					continue;
				if (type.HasGenericParameters)
					continue;
				var moduleName = type.Namespace;
				var name = type.Name;
				if (TypeAggregator.FilterModuleAndName (platform, moduleName, ref name)) {
					yield return MetadataFuncForType (platform, type, entityType);
				}
			}
		}

		static SLImportModules ImportsForPlatform (PlatformName platform, TypeDefinition [] types)
		{
			var imports = new SLImportModules ();
			var modulesUsed = new HashSet<string> ();
			foreach (var type in types) {
				var moduleName = type.Namespace;
				var name = "ThisShouldNeverGetFiltered";
				if (TypeAggregator.FilterModuleAndName (platform, type.Namespace, ref name)) {
					TypeAggregator.RemapModuleAndName (platform, ref moduleName, ref name, TypeType.None);
					if (String.IsNullOrEmpty (moduleName))
						continue;
					if (modulesUsed.Contains (moduleName))
						continue;
					modulesUsed.Add (moduleName);
					imports.Add (new SLImport (moduleName));
				}
			}
			return imports;
		}

		static Dictionary<string, string> TLFunctionsForFile (string path, PlatformName platform)
		{
			var result = new Dictionary<string, string> ();
			using (var stm = new FileStream (path, FileMode.Open, FileAccess.Read, FileShare.Read)) {
				TLFunctionsForStream (stm, platform, result);
			}
			return result;
		}

		static void TLFunctionsForStream (Stream stm, PlatformName platform, Dictionary<string, string> functions)
		{
			List<NListEntry> entries = null;
			List<MachOFile> macho = MachO.Read (stm, null).ToList ();
			MachOFile file = macho [0];

			List<SymTabLoadCommand> symbols = file.load_commands.OfType<SymTabLoadCommand> ().ToList ();
			NListEntryType nlet = symbols [0].nlist [0].EntryType;
			entries = symbols [0].nlist.
				Where ((nle, i) => nle.IsPublic && nle.EntryType == NListEntryType.InSection).ToList ();

			bool isOldVersion = IsOldVersion (entries);

			foreach (var entry in entries) {
				if (!entry.IsSwiftEntryPoint ())
					continue;
				TLDefinition def = null;
				try {
					def = Decomposer.Decompose (entry.str, isOldVersion, Offset (entry));
				} catch { }
				if (def != null) {
					// this skips over privatized names
					var tlf = def as TLFunction;
					if (tlf != null && tlf.Name != null && tlf.Name.Name.Contains ("..."))
						continue;
					if (tlf?.Name != null) {
						if (!functions.ContainsKey (tlf.Name.Name))
							functions.Add (tlf.Name.Name, tlf.MangledName);
					}
				}
			}
		}

		static SLAttribute AvailableAttributeForFunc (PlatformName platform, TypeDefinition type)
		{
			if (type.HasCustomAttributes) {
				foreach (var customAttribute in type.CustomAttributes) {
					if (customAttribute.AttributeType.Name == "IntroducedAttribute") {
						var platformValue = PlatformValueForPlatform (platform);
						if (!customAttribute.HasConstructorArguments)
							continue;
						if (customAttribute.ConstructorArguments [0].Value.ToString () != platformValue)
							continue;
						var attr = AvailableAttributeFromIntroducedAttributeType (platform, customAttribute);
						if (attr != null)
							return attr;
					} else {
						var attributeName = AttributeNameForPlatform (platform);
						if (attributeName == null)
							continue;
						if (customAttribute.AttributeType.Name != attributeName)
							continue;
						var attr = AvailableAttributeFromAttributeType (platform, customAttribute);
						if (attr != null)
							return attr;
					}
				}
			}
			var map = TypeAggregator.AvailableMapForPlatform (platform);
			string available;
			if (map != null && map.TryGetValue (type.FullName, out available))
				return new SLAttribute ("available", true, new SLIdentifier (available));
			return null;
		}

		static ulong Offset (NListEntry entry)
		{
			NListEntry32 nl32 = entry as NListEntry32;
			return nl32 != null ? nl32.n_value : ((NListEntry64)entry).n_value;
		}

		static bool IsOldVersion (List<NListEntry> entries)
		{
			foreach (NListEntry entry in entries) {
				if (entry.str.StartsWith ("__TMd", StringComparison.Ordinal))
					return true;
			}
			return false;
		}

		static SLAttribute AvailableAttributeFromIntroducedAttributeType (PlatformName platform, CustomAttribute customAttribute)
		{
			// if we get here, we have ConstructorArguments
			var argsValues = new List<string> ();
			for (var i = 1; i < customAttribute.ConstructorArguments.Count; i++) {
				var arg = customAttribute.ConstructorArguments [i];
				if (arg.Type.Name != "Int32")
					break;
				argsValues.Add (arg.Value.ToString ());
			}
			if (argsValues.Count == 0)
				return null;
			if (argsValues.Count == 1)
				argsValues.Add ("0");
			return AvailableAttributeFromComponents (platform, argsValues);
		}

		static SLAttribute AvailableAttributeFromAttributeType (PlatformName platform, CustomAttribute customAttribute)
		{
			if (!customAttribute.HasConstructorArguments)
				return null;
			var argsValues = new List<string> ();
			foreach (var arg in customAttribute.ConstructorArguments) {
				if (arg.Type.Name != "Byte")
					break;
				argsValues.Add (arg.Value.ToString ());
			}
			if (argsValues.Count == 0)
				return null;
			if (argsValues.Count == 1)
				argsValues.Add ("0");
			return AvailableAttributeFromComponents (platform, argsValues);
		}

		static SLAttribute AvailableAttributeFromComponents (PlatformName platform, List<string> argsValues)
		{
			var output = new StringBuilder ();
			output.Append (AttributePlatformStringForPlatform (platform)).Append (" ").Append (argsValues [0]);
			for (var i = 1; i < argsValues.Count; i++) {
				output.Append (".").Append (argsValues [i]);
			}
			output.Append (", *");
			return new SLAttribute ("available", true, new SLIdentifier (output.ToString ()));
		}

		static string PlatformValueForPlatform (PlatformName platform)
		{
			switch (platform) {
			case PlatformName.iOS:
				return "2";
			case PlatformName.macOS:
				return "1";
			case PlatformName.tvOS:
				return "4";
			case PlatformName.watchOS:
				return "3";
			default:
				throw new NotImplementedException ($"Unknown platform {platform.ToString ()}");
			}
		}

		static string AttributePlatformStringForPlatform (PlatformName platform)
		{
			switch (platform) {
			case PlatformName.iOS:
				return "iOS";
			case PlatformName.macOS:
				return "macOS";
			case PlatformName.tvOS:
				return "tvOS";
			case PlatformName.watchOS:
				return "watchOS";
			default:
				throw new NotImplementedException ($"Unknown platform type {platform}");
			}
		}

		static string AttributeNameForPlatform (PlatformName platform)
		{
			switch (platform) {
			case PlatformName.iOS:
				return "iOSAttribute";
			case PlatformName.macOS:
				return "MacAttribute";
			case PlatformName.tvOS:
				return "TVAttribute";
			case PlatformName.watchOS:
				return "WatchAttribute";
			default:
				return null;
			}
		}

		static SLIdentifier PlatformCondition(PlatformName platform)
		{
			switch (platform) {
			case PlatformName.iOS: return new SLIdentifier ("os(iOS)");
			case PlatformName.macOS: return new SLIdentifier ("os(macOS)");
			case PlatformName.tvOS: return new SLIdentifier ("os(tvOS)");
			case PlatformName.watchOS: return new SLIdentifier ("os(watchOS)");
			default:
				throw new ArgumentOutOfRangeException (nameof (platform));
			}
		}

		static CSIdentifier PlatformToCSCondition (PlatformName platform)
		{
			switch (platform) {
			case PlatformName.iOS: return new CSIdentifier ("__IOS__");
			case PlatformName.macOS: return new CSIdentifier ("__MACOS__");
			case PlatformName.tvOS: return new CSIdentifier ("__TVOS__");
			case PlatformName.watchOS: return new CSIdentifier ("__WATCHOS__");
			default:
				throw new ArgumentOutOfRangeException (nameof (platform));
			}
		}
		static string PlaformToDirectoryName (PlatformName platform)
		{
			switch (platform) {
			case PlatformName.iOS:
				return "iphoneos";
			case PlatformName.macOS:
				return "macosx";
			case PlatformName.tvOS:
				return "appletvos";
			case PlatformName.watchOS:
				return "watchos";
			default:
				throw new ArgumentOutOfRangeException (nameof (platform), $"Unexpected platform {platform}");
			}
		}

	}
}

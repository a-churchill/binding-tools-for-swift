// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Dynamo.CSLang {
	public class CSGenericTypeDeclaration {
		public CSGenericTypeDeclaration (CSIdentifier name)
		{
			Name = Exceptions.ThrowOnNull (name, nameof (name));
		}

		public CSIdentifier Name { get; private set; }
	}

	public class CSGenericTypeDeclarationCollection : List<CSGenericTypeDeclaration>, ICodeElementSet {
		public CSGenericTypeDeclarationCollection ()
			: base ()
		{
		}

		public IEnumerable<ICodeElement> Elements {
			get {
				if (this.Count > 0) {
					yield return new SimpleElememt ("<");
					bool first = true;
					foreach (CSGenericTypeDeclaration decl in this) {
						if (!first) {
							yield return new SimpleElememt (",", true);
							yield return SimpleElememt.Spacer;
						} else {
							first = false;
						}
						yield return decl.Name;
					}
					yield return new SimpleElememt (">");
				}
			}
		}

		public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

		public event EventHandler<WriteEventArgs> End = (s, e) => { };

		public virtual object BeginWrite (ICodeWriter writer)
		{
			OnBeginWrite (new WriteEventArgs (writer));
			return null;
		}

		protected virtual void OnBeginWrite (WriteEventArgs args)
		{
			Begin (this, args);
		}

		public virtual void Write (ICodeWriter writer, object o)
		{
		}

		public virtual void EndWrite (ICodeWriter writer, object o)
		{
			OnEndWrite (new WriteEventArgs (writer));
		}

		protected virtual void OnEndWrite (WriteEventArgs args)
		{
			End.FireInReverse (this, args);
		}
	}
}

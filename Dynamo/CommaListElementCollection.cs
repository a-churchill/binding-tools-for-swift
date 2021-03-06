// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Dynamo {
	public class CommaListElementCollection<T> : List<T>, ICodeElement where T : ICodeElement {
		public CommaListElementCollection ()
			: this ("", "")
		{
		}


		public CommaListElementCollection (IEnumerable<T> objs)
			: this ("", "", objs)
		{
		}

		public CommaListElementCollection (string prefix, string suffix)
			: base ()
		{
			Prefix = Exceptions.ThrowOnNull (prefix, nameof (prefix));
			Suffix = Exceptions.ThrowOnNull (suffix, nameof (suffix));
		}

		public CommaListElementCollection (string prefix, string suffix, IEnumerable<T> objs, bool newlineAfterEach = false)
			: this (prefix, suffix)
		{
			AddRange (Exceptions.ThrowOnNull (objs, nameof (objs)));
			NewlineAfterEach = newlineAfterEach;
		}


		public string Prefix { get; private set; }
		public string Suffix { get; private set; }
		public bool NewlineAfterEach { get; private set; }

		#region ICodeElem implementation

		public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

		public event EventHandler<WriteEventArgs> End = (s, e) => { };

		public object BeginWrite (ICodeWriter writer)
		{
			OnBegin (new WriteEventArgs (writer));
			return null;
		}

		protected virtual void OnBegin (WriteEventArgs args)
		{
			Begin (this, args);
		}

		public void Write (ICodeWriter writer, object o)
		{
			writer.Write (Prefix, true);
			for (int i = 0; i < Count; i++) {
				this [i].WriteAll (writer);
				if (i < Count - 1)
					writer.Write (", ", true);
				if (NewlineAfterEach && !writer.IsAtLineStart) {
					writer.BeginNewLine (true);
				}
			}
			writer.Write (Suffix, true);
		}

		public void EndWrite (ICodeWriter writer, object o)
		{
			OnEnd (new WriteEventArgs (writer));
		}


		protected virtual void OnEnd (WriteEventArgs args)
		{
			End.FireInReverse (this, args);
		}

		#endregion
	}
}


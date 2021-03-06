// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Dynamo.CSLang {
	public class CSParenthesisExpression : CSBaseExpression {
		public CSParenthesisExpression (ICSExpression within)
		{
			Within = Exceptions.ThrowOnNull (within, "within");
		}

		public ICSExpression Within { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write ('(', true);
			Within.WriteAll (writer);
			writer.Write (')', true);
		}
	}

	public class CSCastExpression : CSBaseExpression, ICSLineable {
		public CSCastExpression (string type, ICSExpression toCast)
			: this (new CSSimpleType (type), toCast)
		{
		}

		public CSCastExpression (CSType type, ICSExpression toCast)
		{
			Type = Exceptions.ThrowOnNull (type, "type");
			ToCast = Exceptions.ThrowOnNull (toCast, "toCast");
		}

		public CSType Type { get; private set; }
		public ICSExpression ToCast { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write ("(", true);
			Type.WriteAll (writer);
			writer.Write (')', true);
			ToCast.WriteAll (writer);
		}
	}
}


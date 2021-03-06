// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using SwiftReflector;

namespace tomwiftytest {
	public static class Extensions {
		public static void Seek (this Stream stm)
		{
			stm.Seek (0, SeekOrigin.Begin);
		}

		public static void CopyTo (this Stream stm, Stream source, int blockSize = 4096)
		{
			if (blockSize <= 0)
				throw new ArgumentOutOfRangeException ("blockSize");
			if (source == null)
				throw new ArgumentNullException ("source");
			byte [] buffer = new byte [blockSize];
			while (true) {
				int bytesRead = source.Read (buffer, 0, blockSize);
				if (bytesRead > 0)
					stm.Write (buffer, 0, bytesRead);
				else
					break;
			}
		}
	}
}


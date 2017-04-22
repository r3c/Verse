﻿using System.IO;
using System.Text;

namespace Verse.Schemas.QueryString
{
	class ReaderState
	{
		public int Current;

		public readonly Encoding Encoding;

		private readonly DecodeError error;

		private int position;

		private readonly StreamReader reader;

		public ReaderState(Stream stream, Encoding encoding, DecodeError error)
		{
			this.Current = 0;
			this.Encoding = encoding;

			this.error = error;
			this.position = 0;
			this.reader = new StreamReader(stream, encoding);

			this.Pull();
		}

		public void Error(string message)
		{
			this.error(this.position, message);
		}

		public void Pull()
		{
			this.Current = this.reader.Read();

			++this.position;
		}
	}
}
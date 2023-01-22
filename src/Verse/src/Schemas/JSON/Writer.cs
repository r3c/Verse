﻿using System.Collections.Generic;
using System.IO;
using System.Text;
using Verse.EncoderDescriptors.Tree;

namespace Verse.Schemas.JSON
{
	internal class Writer : IWriter<WriterState, JSONValue>
	{
		private readonly Encoding encoding;

		private readonly bool omitNull;

		public Writer(Encoding encoding, bool omitNull)
		{
			this.encoding = encoding;
			this.omitNull = omitNull;
		}

		public void Flush(WriterState state)
		{
			state.Flush();
		}

		public WriterState Start(Stream stream, ErrorEvent error)
		{
			return new WriterState(stream, encoding, omitNull);
		}

		public void Stop(WriterState state)
		{
			state.Dispose();
		}

		public void WriteAsArray<TElement>(WriterState state, IEnumerable<TElement> elements,
			WriterCallback<WriterState, JSONValue, TElement> writer)
		{
			if (elements == null)
				WriteAsValue(state, JSONValue.Void);
			else
			{
				state.ArrayBegin();

				foreach (var element in elements)
					writer(this, state, element);

				state.ArrayEnd();
			}
		}

		public void WriteAsObject<TObject>(WriterState state, TObject parent,
			IReadOnlyDictionary<string, WriterCallback<WriterState, JSONValue, TObject>> fields)
		{
			if (parent == null)
				WriteAsValue(state, JSONValue.Void);
			else
			{
				state.ObjectBegin();

				foreach (var field in fields)
				{
					state.Key(field.Key);
					field.Value(this, state, parent);
				}

				state.ObjectEnd();
			}
		}

		public void WriteAsValue(WriterState state, JSONValue value)
		{
			state.Value(value);
		}
	}
}

﻿using System.Collections.Generic;
using System.IO;
using System.Text;
using Verse.EncoderDescriptors.Tree;

namespace Verse.Schemas.JSON
{
	class WriterSession : IWriterSession<WriterState, JSONValue>
	{
		private readonly Encoding encoding;

		private readonly bool omitNull;

		public WriterSession(Encoding encoding, bool omitNull)
		{
			this.encoding = encoding;
			this.omitNull = omitNull;
		}

		public bool Start(Stream stream, EncodeError error, out WriterState state)
		{
			state = new WriterState(stream, this.encoding, this.omitNull);

			return true;
		}

		public void Stop(WriterState state)
		{
			state.Flush();
		}

		public void WriteArray<TEntity>(WriterState state, IEnumerable<TEntity> elements, WriterCallback<WriterState, JSONValue, TEntity> writer)
		{
			if (elements == null)
				this.WriteValue(state, JSONValue.Void);
			else
			{
				state.ArrayBegin();

				foreach (var element in elements)
					writer(this, state, element);

				state.ArrayEnd();
			}
		}

		public void WriteObject<TEntity>(WriterState state, TEntity parent, IReadOnlyDictionary<string, WriterCallback<WriterState, JSONValue, TEntity>> fields)
		{
			if (parent == null)
				this.WriteValue(state, JSONValue.Void);
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

		public void WriteValue(WriterState state, JSONValue value)
		{
			state.Value(value);
		}
	}
}

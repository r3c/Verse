using System;
using System.IO;

namespace Verse.EncoderDescriptors.Recurse
{
	class Encoder<TEntity, TValue, TState> : IEncoder<TEntity>
	{
		#region Events

		public event EncodeError Error;

		#endregion

		#region Attributes

		private readonly IWriter<TEntity, TValue, TState> writer;

		#endregion

		#region Constructors

		public Encoder(IWriter<TEntity, TValue, TState> writer)
		{
			this.writer = writer;
		}

		#endregion

		#region Methods / Public

		public bool Encode(TEntity input, Stream output)
		{
			TState state;

			if (!this.writer.Start(output, this.OnError, out state))
				return false;

			try
			{
				this.writer.WriteEntity(input, state);
			}
			finally
			{
				this.writer.Stop(state);
			}

			return true;
		}

		#endregion

		#region Methods / Private

		private void OnError(int position, string message)
		{
			EncodeError error;

			error = this.Error;

			if (error != null)
				error(position, message);
		}

		#endregion
	}
}
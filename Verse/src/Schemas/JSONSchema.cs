using System;
using System.Text;
using Verse.DecoderDescriptors;
using Verse.EncoderDescriptors;
using Verse.EncoderDescriptors.Tree;
using Verse.Schemas.JSON;

namespace Verse.Schemas
{
	/// <summary>
	/// Schema implementation using JSON format.
	/// </summary>
	/// <typeparam name="TEntity">Entity type</typeparam>
	public class JSONSchema<TEntity> : ISchema<TEntity>
	{
		/// <inheritdoc/>
		public IDecoderDescriptor<TEntity> DecoderDescriptor => this.decoderDescriptor;

		/// <inheritdoc/>
		public IEncoderDescriptor<TEntity> EncoderDescriptor => this.encoderDescriptor;

		private readonly JSONConfiguration configuration;

		private readonly DecoderConverter decoderConverter;

		private readonly TreeDecoderDescriptor<TEntity, ReaderState, JSONValue> decoderDescriptor;

		private readonly EncoderConverter encoderConverter;

		private readonly TreeEncoderDescriptor<TEntity, WriterState, JSONValue> encoderDescriptor;

		/// <summary>
		/// Create new JSON schema using given settings
		/// </summary>
		/// <param name="configuration">Text encoding, ignore null...</param>
		public JSONSchema(JSONConfiguration configuration)
		{
			var encoding = configuration.Encoding ?? new UTF8Encoding(false);
			var writerDefinition = new WriterDefinition<WriterState, JSONValue, TEntity>();

			this.configuration = configuration;
			this.decoderConverter = new DecoderConverter();
			this.encoderConverter = new EncoderConverter();
			this.decoderDescriptor = new TreeDecoderDescriptor<TEntity, ReaderState, JSONValue>(this.decoderConverter, new ReaderSession(encoding), new Reader<TEntity>());
			this.encoderDescriptor = new TreeEncoderDescriptor<TEntity, WriterState, JSONValue>(this.encoderConverter, writerDefinition);
		}

		/// <summary>
		/// Create JSON schema using default UTF8 encoding.
		/// </summary>
		public JSONSchema()
			: this(default)
		{
		}

		/// <inheritdoc/>
		public IDecoder<TEntity> CreateDecoder()
		{
			return this.decoderDescriptor.CreateDecoder();
		}

		/// <inheritdoc/>
		public IEncoder<TEntity> CreateEncoder()
		{
			var encoding = this.configuration.Encoding ?? new UTF8Encoding(false);
			var session = new WriterSession(encoding, this.configuration.OmitNull);

			return this.encoderDescriptor.CreateEncoder(session);
		}

		/// <summary>
		/// Declare decoder to convert JSON native value into target output type.
		/// </summary>
		/// <typeparam name="TOutput">Target output type</typeparam>
		/// <param name="converter">Converter from JSON native value to output type</param>
		public void SetDecoderConverter<TOutput>(Converter<JSONValue, TOutput> converter)
		{
			this.decoderConverter.Set(converter);
		}

		/// <summary>
		/// Declare encoder to convert target input type into JSON native value.
		/// </summary>
		/// <typeparam name="TInput">Target input type</typeparam>
		/// <param name="converter">Converter from input type to JSON native value</param>
		public void SetEncoderConverter<TInput>(Converter<TInput, JSONValue> converter)
		{
			this.encoderConverter.Set(converter);
		}
	}
}
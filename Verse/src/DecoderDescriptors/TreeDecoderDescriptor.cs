using System;
using System.Collections.Generic;
using Verse.DecoderDescriptors.Tree;
using Verse.Lookups;

namespace Verse.DecoderDescriptors
{
	internal class TreeDecoderDescriptor<TState, TNative, TEntity> : IDecoderDescriptor<TEntity>
	{
		private readonly IDecoderConverter<TNative> converter;

		private readonly ReaderDefinition<TState, TNative, TEntity> definition;

		private readonly NameLookup<ReaderCallback<TState, TNative, TEntity>> fields;

		public TreeDecoderDescriptor(IDecoderConverter<TNative> converter, ReaderDefinition<TState, TNative, TEntity> definition)
		{
			this.converter = converter;
			this.definition = definition;
			this.fields = new NameLookup<ReaderCallback<TState, TNative, TEntity>>();
		}

		public IDecoder<TEntity> CreateDecoder(IReaderSession<TState, TNative> session, Func<TEntity> constructor)
		{
			return new TreeDecoder<TState, TNative, TEntity>(session, constructor, this.definition.Callback);
		}

		public IDecoderDescriptor<TField> HasField<TField>(string name, Func<TField> constructor,
			Setter<TEntity, TField> setter, IDecoderDescriptor<TField> descriptor)
		{
			if (!(descriptor is TreeDecoderDescriptor<TState, TNative, TField> ancestor))
				throw new ArgumentOutOfRangeException(nameof(descriptor), "incompatible descriptor type");

			return TreeDecoderDescriptor<TState, TNative, TEntity>.BindField(this.definition, name, this.fields,
				constructor, setter, ancestor);
		}

		public IDecoderDescriptor<TField> HasField<TField>(string name, Func<TField> constructor,
			Setter<TEntity, TField> setter)
		{
			var fieldDefinition = this.definition.Create<TField>();
			var fieldDescriptor = new TreeDecoderDescriptor<TState, TNative, TField>(this.converter, fieldDefinition);

			return TreeDecoderDescriptor<TState, TNative, TEntity>.BindField(this.definition, name, this.fields,
				constructor, setter, fieldDescriptor);
		}

		public IDecoderDescriptor<TEntity> HasField(string name)
		{
			var fieldDefinition = this.definition.Create<TEntity>();
			var fieldDescriptor = new TreeDecoderDescriptor<TState, TNative, TEntity>(this.converter, fieldDefinition);
			var parentFields = this.fields;

			var success = parentFields.ConnectTo(name,
				(IReaderSession<TState, TNative> session, TState state, ref TEntity entity) =>
					fieldDefinition.Callback(session, state, ref entity));

			if (!success)
				throw new InvalidOperationException($"field '{name}' was declared twice on same descriptor");

			this.definition.Callback = (IReaderSession<TState, TNative> session, TState state, ref TEntity target) =>
				session.ReadToObject(state, parentFields, ref target);

			return fieldDescriptor;
		}

		public IDecoderDescriptor<TElement> HasElements<TElement>(Func<TElement> constructor,
			Setter<TEntity, IEnumerable<TElement>> setter, IDecoderDescriptor<TElement> descriptor)
		{
			if (!(descriptor is TreeDecoderDescriptor<TState, TNative, TElement> ancestor))
				throw new ArgumentOutOfRangeException(nameof(descriptor), "incompatible descriptor type");

			return TreeDecoderDescriptor<TState, TNative, TEntity>.BindArray(this.definition, constructor, setter, ancestor);
		}

		public IDecoderDescriptor<TElement> HasElements<TElement>(Func<TElement> constructor,
			Setter<TEntity, IEnumerable<TElement>> setter)
		{
			var elementDefinition = this.definition.Create<TElement>();
			var elementDescriptor =
				new TreeDecoderDescriptor<TState, TNative, TElement>(this.converter, elementDefinition);

			return TreeDecoderDescriptor<TState, TNative, TEntity>.BindArray(this.definition, constructor, setter,
				elementDescriptor);
		}

		public void HasValue<TValue>(Setter<TEntity, TValue> setter)
		{
			var native = this.converter.Get<TValue>();

			this.definition.Callback = (IReaderSession<TState, TNative> session, TState state, ref TEntity entity) =>
			{
				if (!session.ReadToValue(state, out var value))
				{
					entity = default;

					return false;
				}

				// FIXME: support conversion failures
				setter(ref entity, native(value));

				return true;
			};
		}

		public void HasValue()
		{
			var converter = this.converter.Get<TEntity>();

			// FIXME: close duplicate of previous method
			this.definition.Callback = (IReaderSession<TState, TNative> session, TState state, ref TEntity entity) =>
			{
				if (!session.ReadToValue(state, out var value))
				{
					entity = default;

					return false;
				}

				// FIXME: support conversion failures
				entity = converter(value);

				return true;
			};
		}

		private static TreeDecoderDescriptor<TState, TNative, TElement> BindArray<TElement>(
			ReaderDefinition<TState, TNative, TEntity> parentDefinition, Func<TElement> constructor,
			Setter<TEntity, IEnumerable<TElement>> setter,
			TreeDecoderDescriptor<TState, TNative, TElement> elementDescriptor)
		{
			var elementDefinition = elementDescriptor.definition;

			parentDefinition.Callback = (IReaderSession<TState, TNative> session, TState state, ref TEntity entity) =>
			{
				using (var browser =
					new Browser<TElement>(session.ReadToArray(state, constructor, elementDefinition.Callback)))
				{
					setter(ref entity, browser);

					return browser.Finish();
				}
			};

			return elementDescriptor;
		}

		private static TreeDecoderDescriptor<TState, TNative, TField> BindField<TField>(
			ReaderDefinition<TState, TNative, TEntity> parentDefinition, string name,
			NameLookup<ReaderCallback<TState, TNative, TEntity>> parentFields, Func<TField> constructor,
			Setter<TEntity, TField> setter, TreeDecoderDescriptor<TState, TNative, TField> fieldDescriptor)
		{
			var fieldDefinition = fieldDescriptor.definition;
			var success = parentFields.ConnectTo(name,
				(IReaderSession<TState, TNative> session, TState state, ref TEntity entity) =>
				{
					var field = constructor();

					if (!fieldDefinition.Callback(session, state, ref field))
						return false;

					setter(ref entity, field);

					return true;
				});

			if (!success)
				throw new InvalidOperationException($"field '{name}' was declared twice on same descriptor");

			parentDefinition.Callback = (IReaderSession<TState, TNative> session, TState state, ref TEntity target) =>
				session.ReadToObject(state, parentFields, ref target);

			return fieldDescriptor;
		}
	}
}
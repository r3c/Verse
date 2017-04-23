﻿using System;
using Verse.DecoderDescriptors.Abstract;

namespace Verse.DecoderDescriptors.Flat
{
	abstract class FlatReader<TEntity, TState, TValue> : IReader<TEntity, TState>
	{
		protected bool IsValue
		{
			get
			{
				return this.convert != null;
			}
		}

		protected EntityTree<TEntity, TState> Root
		{
			get
			{
				return this.fields;
			}
		}

		private readonly EntityTree<TEntity, TState> fields = new EntityTree<TEntity, TState>();

		private Converter<TValue, TEntity> convert = null;

		public abstract FlatReader<TOther, TState, TValue> Create<TOther>();

		public abstract bool Read(ref TEntity entity, TState state);

		public abstract bool ReadValue(TState state, out TEntity value);

		public void DeclareField(string name, EntityReader<TEntity, TState> enter)
		{
			if (!this.fields.Connect(name, enter))
				throw new InvalidOperationException("can't declare same field '" + name + "' twice on same descriptor");
		}
	
		public void DeclareValue(Converter<TValue, TEntity> convert)
		{
			if (this.convert != null)
				throw new InvalidOperationException("can't declare value twice on same descriptor");

			this.convert = convert;
		}

		protected TEntity ConvertValue(TValue value)
		{
			return this.convert(value);
		}
	}
}

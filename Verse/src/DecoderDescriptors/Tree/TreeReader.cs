﻿using System;
using Verse.DecoderDescriptors.Base;

namespace Verse.DecoderDescriptors.Tree
{
	abstract class TreeReader<TEntity, TState, TValue> : IReader<TEntity, TState>
	{
		protected bool HoldValue => this.convert != null;

	    private Converter<TValue, TEntity> convert = null;

		public abstract BrowserMove<TEntity> Browse(Func<TEntity> constructor, TState state);

		public abstract TreeReader<TField, TState, TValue> HasField<TField>(string name, EntityReader<TEntity, TState> enter);

		public abstract TreeReader<TItem, TState, TValue> HasItems<TItem>(EntityReader<TEntity, TState> enter);

		public abstract bool Read(ref TEntity entity, TState state);

		public void IsValue(Converter<TValue, TEntity> convert)
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
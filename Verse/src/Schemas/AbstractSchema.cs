using System;
using Verse.Tools;

namespace Verse.Schemas
{
	public abstract class AbstractSchema<T> : ISchema<T>
	{
		#region Properties

		public abstract IBuilderDescriptor<T>	BuilderDescriptor
		{
			get;
		}

		public abstract IParserDescriptor<T>	ParserDescriptor
		{
			get;
		}

		#endregion

		#region Methods / Abstract

		public abstract IBuilder<T>	CreateBuilder ();

		public abstract IParser<T>	CreateParser (Func<T> constructor);

		#endregion

		#region Methods / Public

		public IParser<T> CreateParser ()
		{
			return this.CreateParser (Generator.Constructor<T> ());
		}

		#endregion
	}
}

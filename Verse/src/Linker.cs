using System;
using System.Collections.Generic;
using System.Reflection;
using Verse.Resolvers;
using Verse.Generators;

namespace Verse
{
	/// <summary>
	/// Utility class able to scan any type (using reflection) to automatically
	/// declare its fields to a decoder or encoder descriptor.
	/// </summary>
	public static class Linker
	{
		private const BindingFlags DefaultBindings = BindingFlags.Instance | BindingFlags.Public;

		/// <summary>
		/// Describe and create decoder for given schema using reflection on target entity and provided binding flags.
		/// </summary>
		/// <typeparam name="TEntity">Entity type</typeparam>
		/// <param name="schema">Entity schema</param>
		/// <param name="bindings">Binding flags used to filter bound fields and properties</param>
		/// <returns>Entity decoder</returns>
		public static IDecoder<TEntity> CreateDecoder<TEntity>(ISchema<TEntity> schema, BindingFlags bindings)
		{
			if (!Linker.LinkDecoder(schema.DecoderDescriptor, bindings, new Dictionary<Type, object>()))
				throw new ArgumentException($"can't link decoder for type '{typeof(TEntity)}'", nameof(schema));

			return schema.CreateDecoder(ConstructorGenerator.CreateConstructor<TEntity>(bindings));
		}

		/// <summary>
		/// Describe and create decoder for given schema using reflection on target entity. Only public instance fields
		/// and properties are linked.
		/// </summary>
		/// <typeparam name="TEntity">Entity type</typeparam>
		/// <param name="schema">Entity schema</param>
		/// <returns>Entity decoder</returns>
		public static IDecoder<TEntity> CreateDecoder<TEntity>(ISchema<TEntity> schema)
		{
			return Linker.CreateDecoder(schema, Linker.DefaultBindings);
		}

		/// <summary>
		/// Describe and create encoder for given schema using reflection on target entity and provided binding flags.
		/// </summary>
		/// <typeparam name="TEntity">Entity type</typeparam>
		/// <param name="schema">Entity schema</param>
		/// <param name="bindings">Binding flags used to filter bound fields and properties</param>
		/// <returns>Entity encoder</returns>
		public static IEncoder<TEntity> CreateEncoder<TEntity>(ISchema<TEntity> schema, BindingFlags bindings)
		{
			if (!Linker.LinkEncoder(schema.EncoderDescriptor, bindings, new Dictionary<Type, object>()))
				throw new ArgumentException($"can't link encoder for type '{typeof(TEntity)}'", nameof(schema));

			return schema.CreateEncoder();
		}

		/// <summary>
		/// Describe and create encoder for given schema using reflection on target entity. Only public instance fields
		/// and properties are linked.
		/// </summary>
		/// <typeparam name="TEntity">Entity type</typeparam>
		/// <param name="schema">Entity schema</param>
		/// <returns>Entity encoder</returns>
		public static IEncoder<TEntity> CreateEncoder<TEntity>(ISchema<TEntity> schema)
		{
			return Linker.CreateEncoder(schema, Linker.DefaultBindings);
		}

		private static bool LinkDecoder<T>(IDecoderDescriptor<T> descriptor, BindingFlags bindings,
			IDictionary<Type, object> parents)
		{
			var entityType = typeof(T);

			parents[entityType] = descriptor;

			if (Linker.TryLinkDecoderAsValue(descriptor))
				return true;

			// Bind descriptor as an array of target type is also array
			if (entityType.IsArray)
			{
				var element = entityType.GetElementType();
				var setter = MethodResolver
					.Create<Func<Setter<object[], IEnumerable<object>>>>(() =>
						SetterGenerator.CreateFromEnumerable<object>())
					.SetGenericArguments(element)
					.Invoke(null);

				return Linker.LinkDecoderAsArray(descriptor, bindings, element, setter, parents);
			}

			// Try to bind descriptor as an array if target type IEnumerable<>
			foreach (var interfaceType in entityType.GetInterfaces())
			{
				// Make sure that interface is IEnumerable<T> and store typeof(T)
				if (!TypeResolver.Create(interfaceType)
					.HasSameDefinitionThan<IEnumerable<object>>(out var interfaceTypeArguments))
					continue;

				var elementType = interfaceTypeArguments[0];

				// Search constructor compatible with IEnumerable<>
				foreach (var constructor in entityType.GetConstructors())
				{
					var parameters = constructor.GetParameters();

					if (parameters.Length != 1)
						continue;

					var parameterType = parameters[0].ParameterType;

					if (!TypeResolver.Create(parameterType)
						    .HasSameDefinitionThan<IEnumerable<object>>(out var parameterArguments) ||
					    parameterArguments[0] != elementType)
						continue;

					var setter = MethodResolver
						.Create<Func<ConstructorInfo, Setter<object, object>>>(c =>
							SetterGenerator.CreateFromConstructor<object, object>(c))
						.SetGenericArguments(entityType, parameterType)
						.Invoke(null, constructor);

					return Linker.LinkDecoderAsArray(descriptor, bindings, elementType, setter, parents);
				}
			}

			// Bind readable and writable instance properties
			foreach (var property in entityType.GetProperties(bindings))
			{
				if (property.GetGetMethod() == null || property.GetSetMethod() == null ||
				    property.Attributes.HasFlag(PropertyAttributes.SpecialName))
					continue;

				var setter = MethodResolver
					.Create<Func<PropertyInfo, Setter<object, object>>>(p =>
						SetterGenerator.CreateFromProperty<object, object>(p))
					.SetGenericArguments(entityType, property.PropertyType)
					.Invoke(null, property);

				if (!Linker.LinkDecoderAsObject(descriptor, bindings, property.PropertyType, property.Name, setter,
					parents))
					return false;
			}

			// Bind public instance fields
			foreach (var field in entityType.GetFields(bindings))
			{
				if (field.Attributes.HasFlag(FieldAttributes.SpecialName))
					continue;

				var setter = MethodResolver
					.Create<Func<FieldInfo, Setter<object, object>>>(f =>
						SetterGenerator.CreateFromField<object, object>(f))
					.SetGenericArguments(entityType, field.FieldType)
					.Invoke(null, field);

				if (!Linker.LinkDecoderAsObject(descriptor, bindings, field.FieldType, field.Name, setter, parents))
					return false;
			}

			return true;
		}

		private static bool LinkDecoderAsArray<TEntity>(IDecoderDescriptor<TEntity> descriptor, BindingFlags bindings,
			Type type, object setter, IDictionary<Type, object> parents)
		{
			var constructor = MethodResolver
				.Create<Func<object>>(() => ConstructorGenerator.CreateConstructor<object>(bindings))
				.SetGenericArguments(type)
				.Invoke(null);

			if (parents.TryGetValue(type, out var recurse))
			{
				MethodResolver
					.Create<Func<IDecoderDescriptor<TEntity>, Func<object>, Setter<TEntity, IEnumerable<object>>,
						IDecoderDescriptor<object>, IDecoderDescriptor<object>>>((d, c, s, p) => d.HasElements(c, s, p))
					.SetGenericArguments(type)
					.Invoke(descriptor, constructor, setter, recurse);

				return true;
			}

			var itemDescriptor = MethodResolver
				.Create<Func<IDecoderDescriptor<TEntity>, Func<object>, Setter<TEntity, IEnumerable<object>>,
					IDecoderDescriptor<object>>>((d, c, s) => d.HasElements(c, s))
				.SetGenericArguments(type)
				.Invoke(descriptor, constructor, setter);

			var result = MethodResolver
				.Create<Func<IDecoderDescriptor<object>, BindingFlags, Dictionary<Type, object>, bool>>((d, f, p) =>
					Linker.LinkDecoder(d, f, p))
				.SetGenericArguments(type)
				.Invoke(null, itemDescriptor, bindings, parents);

			return result is bool success && success;
		}

		private static bool LinkDecoderAsObject<TEntity>(IDecoderDescriptor<TEntity> descriptor, BindingFlags bindings,
			Type type, string name, object setter, IDictionary<Type, object> parents)
		{
			var constructor = MethodResolver
				.Create<Func<object>>(() => ConstructorGenerator.CreateConstructor<object>(bindings))
				.SetGenericArguments(type)
				.Invoke(null);

			if (parents.TryGetValue(type, out var parent))
			{
				MethodResolver
					.Create<Func<IDecoderDescriptor<TEntity>, string, Func<object>, Setter<TEntity, object>,
						IDecoderDescriptor<object>, IDecoderDescriptor<object>>>((d, n, c, s, p) =>
						d.HasField(n, c, s, p))
					.SetGenericArguments(type)
					.Invoke(descriptor, name, constructor, setter, parent);

				return true;
			}

			var fieldDescriptor = MethodResolver
				.Create<Func<IDecoderDescriptor<TEntity>, string, Func<object>, Setter<TEntity, object>,
					IDecoderDescriptor<object>>>((d, n, c, s) => d.HasField(n, c, s))
				.SetGenericArguments(type)
				.Invoke(descriptor, name, constructor, setter);

			var result = MethodResolver
				.Create<Func<IDecoderDescriptor<object>, BindingFlags, Dictionary<Type, object>, bool>>((d, f, p) =>
					Linker.LinkDecoder(d, f, p))
				.SetGenericArguments(type)
				.Invoke(null, fieldDescriptor, bindings, parents);

			return result is bool success && success;
		}

		private static bool LinkEncoder<T>(IEncoderDescriptor<T> descriptor, BindingFlags bindings,
			IDictionary<Type, object> parents)
		{
			var entityType = typeof(T);

			parents[entityType] = descriptor;

			if (Linker.TryLinkEncoderAsValue(descriptor))
				return true;

			// Bind descriptor as an array of target type is also array
			if (entityType.IsArray)
			{
				var element = entityType.GetElementType();
				var getter = MethodResolver
					.Create<Func<Func<object, object>>>(() => GetterGenerator.CreateIdentity<object>())
					.SetGenericArguments(typeof(IEnumerable<>).MakeGenericType(element))
					.Invoke(null);

				return Linker.LinkEncoderAsArray(descriptor, bindings, element, getter, parents);
			}

			// Try to bind descriptor as an array if target type IEnumerable<>
			foreach (var interfaceType in entityType.GetInterfaces())
			{
				// Make sure that interface is IEnumerable<T> and store typeof(T)
				if (!TypeResolver.Create(interfaceType).HasSameDefinitionThan<IEnumerable<object>>(out var arguments))
					continue;

				var elementType = arguments[0];
				var getter = MethodResolver
					.Create<Func<Func<object, object>>>(() => GetterGenerator.CreateIdentity<object>())
					.SetGenericArguments(typeof(IEnumerable<>).MakeGenericType(elementType))
					.Invoke(null);

				return Linker.LinkEncoderAsArray(descriptor, bindings, elementType, getter, parents);
			}

			// Bind readable and writable instance properties
			foreach (var property in entityType.GetProperties(bindings))
			{
				if (property.GetGetMethod() == null || property.GetSetMethod() == null ||
				    property.Attributes.HasFlag(PropertyAttributes.SpecialName))
					continue;

				var getter = MethodResolver
					.Create<Func<PropertyInfo, Func<object, object>>>(p =>
						GetterGenerator.CreateFromProperty<object, object>(p))
					.SetGenericArguments(entityType, property.PropertyType)
					.Invoke(null, property);

				if (!Linker.LinkEncoderAsObject(descriptor, bindings, property.PropertyType, property.Name, getter,
					parents))
					return false;
			}

			// Bind public instance fields
			foreach (var field in entityType.GetFields(bindings))
			{
				if (field.Attributes.HasFlag(FieldAttributes.SpecialName))
					continue;

				var getter = MethodResolver
					.Create<Func<FieldInfo, Func<object, object>>>(f =>
						GetterGenerator.CreateFromField<object, object>(f))
					.SetGenericArguments(entityType, field.FieldType)
					.Invoke(null, field);

				if (!Linker.LinkEncoderAsObject(descriptor, bindings, field.FieldType, field.Name, getter, parents))
					return false;
			}

			return true;
		}

		private static bool LinkEncoderAsArray<TEntity>(IEncoderDescriptor<TEntity> descriptor, BindingFlags bindings,
			Type type,
			object getter, IDictionary<Type, object> parents)
		{
			if (parents.TryGetValue(type, out var recurse))
			{
				MethodResolver
					.Create<Func<IEncoderDescriptor<TEntity>, Func<TEntity, IEnumerable<object>>,
						IEncoderDescriptor<object>, IEncoderDescriptor<object>>>((d, a, p) => d.HasElements(a, p))
					.SetGenericArguments(type)
					.Invoke(descriptor, getter, recurse);

				return true;
			}

			var itemDescriptor = MethodResolver
				.Create<Func<IEncoderDescriptor<TEntity>, Func<TEntity, IEnumerable<object>>, IEncoderDescriptor<object>
				>>((d, a) => d.HasElements(a))
				.SetGenericArguments(type)
				.Invoke(descriptor, getter);

			var result = MethodResolver
				.Create<Func<IEncoderDescriptor<object>, BindingFlags, Dictionary<Type, object>, bool>>((d, f, p) =>
					Linker.LinkEncoder(d, f, p))
				.SetGenericArguments(type)
				.Invoke(null, itemDescriptor, bindings, parents);

			return result is bool success && success;
		}

		private static bool LinkEncoderAsObject<TEntity>(IEncoderDescriptor<TEntity> descriptor, BindingFlags bindings,
			Type type, string name, object getter, IDictionary<Type, object> parents)
		{
			if (parents.TryGetValue(type, out var recurse))
			{
				MethodResolver
					.Create<Func<IEncoderDescriptor<TEntity>, string, Func<TEntity, object>, IEncoderDescriptor<object>,
						IEncoderDescriptor<object>>>((d, n, a, p) => d.HasField(n, a, p))
					.SetGenericArguments(type)
					.Invoke(descriptor, name, getter, recurse);

				return true;
			}

			var fieldDescriptor = MethodResolver
				.Create<Func<IEncoderDescriptor<TEntity>, string, Func<TEntity, object>, IEncoderDescriptor<object>>>(
					(d, n, a) => d.HasField(n, a))
				.SetGenericArguments(type)
				.Invoke(descriptor, name, getter);

			var result = MethodResolver
				.Create<Func<IEncoderDescriptor<object>, BindingFlags, Dictionary<Type, object>, bool>>((d, f, p) =>
					Linker.LinkEncoder(d, f, p))
				.SetGenericArguments(type)
				.Invoke(null, fieldDescriptor, bindings, parents);

			return result is bool success && success;
		}

		private static bool TryLinkDecoderAsValue<TEntity>(IDecoderDescriptor<TEntity> descriptor)
		{
			try
			{
				descriptor.HasValue();

				return true;
			}
			catch (InvalidCastException)
			{
				// Invalid cast exception being thrown means binding fails
				return false;
			}
		}

		private static bool TryLinkEncoderAsValue<TEntity>(IEncoderDescriptor<TEntity> descriptor)
		{
			try
			{
				descriptor.HasValue();

				return true;
			}
			catch (InvalidCastException)
			{
				// Invalid cast exception being thrown means binding fails
				return false;
			}
		}
	}
}

using System;

namespace Verse.Resolvers;

internal readonly struct TypeResolver
{
    private readonly Type _type;

    public static TypeResolver Create(Type type)
    {
        return new TypeResolver(type);
    }

    private TypeResolver(Type type)
    {
        _type = type;
    }

    /// <Summary>
    /// Check whether type is a generic type with same type definition than
    /// type argument `TGeneric` and return its type arguments, e.g. when
    /// called with `IEnumerable&lt;object&gt;` as `TGeneric` check that type is
    /// `IEnumerable&lt;U&gt;` and returns `{ typeof(U) }` as `arguments`.
    /// </Summary>
    public bool HasSameDefinitionThan<TGeneric>(out Type[] arguments)
    {
        var expected = typeof(TGeneric);

        if (!expected.IsGenericType)
            throw new InvalidOperationException("type is not generic");

        if (expected.GetGenericArguments().Length != 1)
            throw new InvalidOperationException("type doesn't have one generic argument");

        var definition = expected.GetGenericTypeDefinition();

        if (!_type.IsGenericType || _type.GetGenericTypeDefinition() != definition)
        {
            arguments = Array.Empty<Type>();

            return false;
        }

        arguments = _type.GetGenericArguments();

        return true;
    }
}
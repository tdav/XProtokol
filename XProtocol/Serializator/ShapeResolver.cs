using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace XProtocol.Serializator
{
    internal static class ShapeResolver
    {
        public static FieldShape Resolve(Type t, HashSet<Type> visiting)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (visiting == null) throw new ArgumentNullException(nameof(visiting));

            if (t == typeof(string))
            {
                return StringShape.Instance;
            }

            if (t.IsValueType)
            {
                return new ValueShape(t);
            }

            if (t.IsArray)
            {
                if (t.GetArrayRank() != 1)
                {
                    throw new InvalidOperationException(
                        $"Multi-dimensional array {t.Name} is not supported. Use jagged arrays instead.");
                }
                var elementType = t.GetElementType();
                var elementShape = Resolve(elementType, visiting);
                return new ArrayShape(elementType, elementShape);
            }

            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.List<>))
            {
                var elementType = t.GetGenericArguments()[0];
                var elementShape = Resolve(elementType, visiting);
                return new ListShape(elementType, elementShape);
            }

            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(System.Collections.Generic.Dictionary<,>))
            {
                var genericArgs = t.GetGenericArguments();
                var keyType = genericArgs[0];
                var valueType = genericArgs[1];

                if (!(keyType.IsValueType || keyType == typeof(string)))
                {
                    throw new InvalidOperationException(
                        $"Dictionary<{keyType.Name}, {valueType.Name}>: key must be value-type or string (got {keyType.Name}).");
                }

                var keyShape = Resolve(keyType, visiting);
                var valueShape = Resolve(valueType, visiting);
                return new DictShape(keyType, valueType, keyShape, valueShape);
            }

            throw new InvalidOperationException($"Type {t.Name} is not supported.");
        }

        public static FieldDescriptor[] BuildDescriptors(Type t, HashSet<Type> visiting)
        {
            if (t == null) throw new ArgumentNullException(nameof(t));
            if (visiting == null) throw new ArgumentNullException(nameof(visiting));

            var fields = new List<FieldInfo>();
            for (var current = t; current != null && current != typeof(object); current = current.BaseType)
            {
                fields.AddRange(
                    current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                           .Where(f => !f.IsLiteral));
            }

            var sorted = fields.OrderBy(f => f.MetadataToken).ToArray();

            if (sorted.Length > byte.MaxValue)
            {
                throw new InvalidOperationException($"{t.Name} has more than {byte.MaxValue} fields.");
            }

            var result = new FieldDescriptor[sorted.Length];
            for (int i = 0; i < sorted.Length; i++)
            {
                var f = sorted[i];
                FieldShape shape;
                try
                {
                    shape = Resolve(f.FieldType, visiting);
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("is not supported"))
                {
                    throw new InvalidOperationException(
                        $"{f.DeclaringType.Name}.{f.Name}: only value-type fields and string are supported (got {f.FieldType.Name}).",
                        ex);
                }
                result[i] = new FieldDescriptor(f, shape);
            }
            return result;
        }
    }
}

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

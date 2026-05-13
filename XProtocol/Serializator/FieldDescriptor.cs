using System;
using System.Linq.Expressions;
using System.Reflection;

namespace XProtocol.Serializator
{
    internal enum FieldKind
    {
        ValueType,
        String
    }

    internal sealed class FieldDescriptor
    {
        public FieldInfo Field { get; }
        public FieldKind Kind { get; }
        public Func<object, object> Getter { get; }
        public Action<object, object> Setter { get; }
        public Func<object, string> StringGetter { get; }
        public Action<object, string> StringSetter { get; }

        public FieldDescriptor(FieldInfo field)
        {
            this.Field = field;

            if (field.FieldType == typeof(string))
            {
                this.Kind = FieldKind.String;
                this.StringGetter = BuildStringGetter(field);
                this.StringSetter = BuildStringSetter(field);
            }
            else if (field.FieldType.IsValueType)
            {
                this.Kind = FieldKind.ValueType;
                this.Getter = BuildGetter(field);
                this.Setter = BuildSetter(field);
            }
            else
            {
                throw new InvalidOperationException(
                    $"{field.DeclaringType.Name}.{field.Name}: only value-type fields and string are supported (got {field.FieldType.Name}).");
            }
        }

        private static Func<object, object> BuildGetter(FieldInfo f)
        {
            var p = Expression.Parameter(typeof(object), "o");
            var body = Expression.Convert(
                Expression.Field(Expression.Convert(p, f.DeclaringType), f),
                typeof(object));
            return Expression.Lambda<Func<object, object>>(body, p).Compile();
        }

        private static Action<object, object> BuildSetter(FieldInfo f)
        {
            var p = Expression.Parameter(typeof(object), "o");
            var v = Expression.Parameter(typeof(object), "v");
            var body = Expression.Assign(
                Expression.Field(Expression.Convert(p, f.DeclaringType), f),
                Expression.Convert(v, f.FieldType));
            return Expression.Lambda<Action<object, object>>(body, p, v).Compile();
        }

        private static Func<object, string> BuildStringGetter(FieldInfo f)
        {
            var p = Expression.Parameter(typeof(object), "o");
            var body = Expression.Field(Expression.Convert(p, f.DeclaringType), f);
            return Expression.Lambda<Func<object, string>>(body, p).Compile();
        }

        private static Action<object, string> BuildStringSetter(FieldInfo f)
        {
            var p = Expression.Parameter(typeof(object), "o");
            var v = Expression.Parameter(typeof(string), "v");
            var body = Expression.Assign(
                Expression.Field(Expression.Convert(p, f.DeclaringType), f), v);
            return Expression.Lambda<Action<object, string>>(body, p, v).Compile();
        }
    }
}

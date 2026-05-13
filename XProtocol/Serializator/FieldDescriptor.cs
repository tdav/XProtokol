using System;
using System.Linq.Expressions;
using System.Reflection;

namespace XProtocol.Serializator
{
    internal sealed class FieldDescriptor
    {
        public FieldInfo Field { get; }
        public FieldShape Shape { get; }
        public Func<object, object> Getter { get; }
        public Action<object, object> Setter { get; }

        public FieldDescriptor(FieldInfo field, FieldShape shape)
        {
            this.Field = field ?? throw new ArgumentNullException(nameof(field));
            this.Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            this.Getter = BuildGetter(field);
            this.Setter = BuildSetter(field);
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
    }
}

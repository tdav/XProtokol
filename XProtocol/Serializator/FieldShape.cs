using System;

namespace XProtocol.Serializator
{
    internal abstract class FieldShape
    {
    }

    internal sealed class ValueShape : FieldShape
    {
        public Type ClrType { get; }

        public ValueShape(Type clrType)
        {
            this.ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
        }
    }

    internal sealed class StringShape : FieldShape
    {
        public static readonly StringShape Instance = new StringShape();
        private StringShape() { }
    }

    internal sealed class ArrayShape : FieldShape
    {
        public Type ElementClrType { get; }
        public FieldShape Element { get; }

        public ArrayShape(Type elementClrType, FieldShape element)
        {
            this.ElementClrType = elementClrType ?? throw new ArgumentNullException(nameof(elementClrType));
            this.Element = element ?? throw new ArgumentNullException(nameof(element));
        }
    }

    internal sealed class ListShape : FieldShape
    {
        public Type ElementClrType { get; }
        public FieldShape Element { get; }

        public ListShape(Type elementClrType, FieldShape element)
        {
            this.ElementClrType = elementClrType ?? throw new ArgumentNullException(nameof(elementClrType));
            this.Element = element ?? throw new ArgumentNullException(nameof(element));
        }
    }

    internal sealed class DictShape : FieldShape
    {
        public Type KeyClrType { get; }
        public Type ValueClrType { get; }
        public FieldShape Key { get; }
        public FieldShape Value { get; }

        public DictShape(Type keyClrType, Type valueClrType, FieldShape key, FieldShape value)
        {
            this.KeyClrType = keyClrType ?? throw new ArgumentNullException(nameof(keyClrType));
            this.ValueClrType = valueClrType ?? throw new ArgumentNullException(nameof(valueClrType));
            this.Key = key ?? throw new ArgumentNullException(nameof(key));
            this.Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    internal sealed class NestedShape : FieldShape
    {
        public Type ClrType { get; }
        public FieldDescriptor[] Fields { get; }

        public NestedShape(Type clrType, FieldDescriptor[] fields)
        {
            this.ClrType = clrType ?? throw new ArgumentNullException(nameof(clrType));
            this.Fields = fields ?? throw new ArgumentNullException(nameof(fields));
        }
    }
}

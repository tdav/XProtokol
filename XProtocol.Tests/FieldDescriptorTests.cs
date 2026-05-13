using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class FieldDescriptorTests
    {
        [Test]
        public async Task Descriptor_ForValueTypeField_HasValueShape()
        {
            var f = typeof(SimpleDto).GetField(nameof(SimpleDto.A), BindingFlags.Instance | BindingFlags.Public);
            var shape = ShapeResolver.Resolve(f.FieldType, new HashSet<Type>());
            var d = new FieldDescriptor(f, shape);

            await Assert.That(d.Shape).IsTypeOf<ValueShape>();
            await Assert.That(((ValueShape)d.Shape).ClrType).IsEqualTo(typeof(int));
            await Assert.That(d.Getter).IsNotNull();
            await Assert.That(d.Setter).IsNotNull();
        }

        [Test]
        public async Task Descriptor_ForStringField_HasStringShape()
        {
            var f = typeof(StringDto).GetField(nameof(StringDto.S), BindingFlags.Instance | BindingFlags.Public);
            var shape = ShapeResolver.Resolve(f.FieldType, new HashSet<Type>());
            var d = new FieldDescriptor(f, shape);

            await Assert.That(d.Shape).IsTypeOf<StringShape>();
        }

        [Test]
        public async Task GetterSetter_RoundtripsString()
        {
            var f = typeof(StringDto).GetField(nameof(StringDto.S), BindingFlags.Instance | BindingFlags.Public);
            var shape = ShapeResolver.Resolve(f.FieldType, new HashSet<Type>());
            var d = new FieldDescriptor(f, shape);

            var obj = new StringDto();
            d.Setter(obj, "hello");

            await Assert.That((string)d.Getter(obj)).IsEqualTo("hello");
        }

        [Test]
        public async Task GetterSetter_RoundtripsInt()
        {
            var f = typeof(SimpleDto).GetField(nameof(SimpleDto.A), BindingFlags.Instance | BindingFlags.Public);
            var shape = ShapeResolver.Resolve(f.FieldType, new HashSet<Type>());
            var d = new FieldDescriptor(f, shape);

            var obj = new SimpleDto();
            d.Setter(obj, 42);

            await Assert.That((int)d.Getter(obj)).IsEqualTo(42);
        }
    }
}

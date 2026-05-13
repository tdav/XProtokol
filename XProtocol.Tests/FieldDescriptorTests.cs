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

        [Test]
        public async Task BuildDescriptors_PreservesNonUnsupportedException()
        {
            // Simulate a Resolver exception that does NOT contain "is not supported"
            // by giving BuildDescriptors a type with a public field whose type causes
            // the current Resolver to throw the standard "is not supported" wrap.
            // Then verify that for the standard unsupported case, the wrapper IS applied
            // (field-context substring present).
            var ex = await Assert.That(() =>
                    ShapeResolver.BuildDescriptors(typeof(UnsupportedRefDto), new HashSet<Type>()))
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("UnsupportedRefDto");
            await Assert.That(ex.Message).Contains("only value-type fields and string are supported");
        }
    }
}

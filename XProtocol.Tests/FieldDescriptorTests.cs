using System;
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
        public async Task Descriptor_ForValueTypeField_HasValueTypeKind()
        {
            var f = typeof(SimpleDto).GetField(nameof(SimpleDto.A), BindingFlags.Instance | BindingFlags.Public);
            var d = new FieldDescriptor(f);

            await Assert.That(d.Kind).IsEqualTo(FieldKind.ValueType);
            await Assert.That(d.Getter).IsNotNull();
            await Assert.That(d.Setter).IsNotNull();
        }

        [Test]
        public async Task Descriptor_ForStringField_HasStringKind()
        {
            var f = typeof(StringDto).GetField(nameof(StringDto.S), BindingFlags.Instance | BindingFlags.Public);
            var d = new FieldDescriptor(f);

            await Assert.That(d.Kind).IsEqualTo(FieldKind.String);
            await Assert.That(d.StringGetter).IsNotNull();
            await Assert.That(d.StringSetter).IsNotNull();
        }

        [Test]
        public async Task Descriptor_ForUnsupportedRefType_Throws()
        {
            var f = typeof(BadDtoWithReferenceField).GetField(
                nameof(BadDtoWithReferenceField.Bad),
                BindingFlags.Instance | BindingFlags.Public);

            var ex = await Assert.That(() => new FieldDescriptor(f))
                .ThrowsExactly<InvalidOperationException>();

            await Assert.That(ex.Message).Contains("only value-type fields");
        }

        [Test]
        public async Task StringGetterSetter_RoundtripsValue()
        {
            var f = typeof(StringDto).GetField(nameof(StringDto.S), BindingFlags.Instance | BindingFlags.Public);
            var d = new FieldDescriptor(f);

            var obj = new StringDto();
            d.StringSetter(obj, "hello");

            await Assert.That(d.StringGetter(obj)).IsEqualTo("hello");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class FieldShapeResolverTests
    {
        [Test]
        public async Task Resolve_IntArray_ReturnsArrayShapeOfValueInt()
        {
            var shape = ShapeResolver.Resolve(typeof(int[]), new HashSet<Type>());

            await Assert.That(shape).IsTypeOf<ArrayShape>();
            var arr = (ArrayShape)shape;
            await Assert.That(arr.ElementClrType).IsEqualTo(typeof(int));
            await Assert.That(arr.Element).IsTypeOf<ValueShape>();
        }

        [Test]
        public async Task Resolve_StringArray_ReturnsArrayShapeOfString()
        {
            var shape = ShapeResolver.Resolve(typeof(string[]), new HashSet<Type>());

            var arr = (ArrayShape)shape;
            await Assert.That(arr.Element).IsTypeOf<StringShape>();
        }

        [Test]
        public async Task Resolve_TwoDimArray_Throws()
        {
            var ex = await Assert.That(() => ShapeResolver.Resolve(typeof(int[,]), new HashSet<Type>()))
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("is not supported");
        }

        [Test]
        public async Task Resolve_ListOfDouble_ReturnsListShape()
        {
            var shape = ShapeResolver.Resolve(typeof(System.Collections.Generic.List<double>), new HashSet<Type>());

            await Assert.That(shape).IsTypeOf<ListShape>();
            var lst = (ListShape)shape;
            await Assert.That(lst.ElementClrType).IsEqualTo(typeof(double));
            await Assert.That(lst.Element).IsTypeOf<ValueShape>();
        }

        [Test]
        public async Task Resolve_ListOfString_ReturnsListOfString()
        {
            var shape = ShapeResolver.Resolve(typeof(System.Collections.Generic.List<string>), new HashSet<Type>());

            var lst = (ListShape)shape;
            await Assert.That(lst.Element).IsTypeOf<StringShape>();
        }

        [Test]
        public async Task Resolve_DictIntString_ReturnsDictShape()
        {
            var shape = ShapeResolver.Resolve(
                typeof(System.Collections.Generic.Dictionary<int, string>),
                new HashSet<Type>());

            await Assert.That(shape).IsTypeOf<DictShape>();
            var d = (DictShape)shape;
            await Assert.That(d.KeyClrType).IsEqualTo(typeof(int));
            await Assert.That(d.ValueClrType).IsEqualTo(typeof(string));
            await Assert.That(d.Key).IsTypeOf<ValueShape>();
            await Assert.That(d.Value).IsTypeOf<StringShape>();
        }

        [Test]
        public async Task Resolve_DictStringInt_StringKeyAllowed()
        {
            var shape = ShapeResolver.Resolve(
                typeof(System.Collections.Generic.Dictionary<string, int>),
                new HashSet<Type>());

            var d = (DictShape)shape;
            await Assert.That(d.Key).IsTypeOf<StringShape>();
        }

        [Test]
        public async Task Resolve_DictWithArrayKey_Throws()
        {
            var ex = await Assert.That(() => ShapeResolver.Resolve(
                    typeof(System.Collections.Generic.Dictionary<int[], int>),
                    new HashSet<Type>()))
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("key must be value-type or string");
        }

        [Test]
        public async Task Resolve_DictWithListKey_Throws()
        {
            var ex = await Assert.That(() => ShapeResolver.Resolve(
                    typeof(System.Collections.Generic.Dictionary<System.Collections.Generic.List<int>, int>),
                    new HashSet<Type>()))
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("key must be value-type or string");
        }
    }
}

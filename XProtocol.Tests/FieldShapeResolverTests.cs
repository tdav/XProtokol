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
    }
}

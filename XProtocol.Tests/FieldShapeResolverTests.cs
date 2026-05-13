using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using XProtocol.Serializator;

namespace XProtocol.Tests
{
    public class NestedDtoNeedsFields
    {
        public int X;
    }

    public class NestedDtoNoFields
    {
    }

    public class NestedDtoNoPublicCtor
    {
        public int X;
        public NestedDtoNoPublicCtor(int x) { this.X = x; }
    }

    public class NestedDtoCycleA
    {
        public NestedDtoCycleB B;
    }

    public class NestedDtoCycleB
    {
        public NestedDtoCycleA A;
    }

    public class NestedDtoSelfCycle
    {
        public NestedDtoSelfCycle Child;
    }

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

        [Test]
        public async Task Resolve_NestedDto_ReturnsNestedShapeWithDescriptors()
        {
            var shape = ShapeResolver.Resolve(typeof(NestedDtoNeedsFields), new HashSet<Type>());

            await Assert.That(shape).IsTypeOf<NestedShape>();
            var n = (NestedShape)shape;
            await Assert.That(n.ClrType).IsEqualTo(typeof(NestedDtoNeedsFields));
            await Assert.That(n.Fields.Length).IsEqualTo(1);
            await Assert.That(n.Fields[0].Shape).IsTypeOf<ValueShape>();
        }

        [Test]
        public async Task Resolve_EmptyNestedDto_Throws()
        {
            var ex = await Assert.That(() => ShapeResolver.Resolve(typeof(NestedDtoNoFields), new HashSet<Type>()))
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("nested DTO must have at least one serialisable field");
        }

        [Test]
        public async Task Resolve_NestedDtoWithoutPublicCtor_Throws()
        {
            var ex = await Assert.That(() => ShapeResolver.Resolve(typeof(NestedDtoNoPublicCtor), new HashSet<Type>()))
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("public parameterless constructor");
        }

        [Test]
        public async Task Resolve_MutualCycle_Throws()
        {
            var ex = await Assert.That(() => ShapeResolver.Resolve(typeof(NestedDtoCycleA), new HashSet<Type>()))
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("Cycle detected");
        }

        [Test]
        public async Task Resolve_SelfCycle_Throws()
        {
            var ex = await Assert.That(() => ShapeResolver.Resolve(typeof(NestedDtoSelfCycle), new HashSet<Type>()))
                .ThrowsExactly<InvalidOperationException>();
            await Assert.That(ex.Message).Contains("Cycle detected");
        }

        [Test]
        public async Task Resolve_HashSet_Throws()
        {
            var ex = await Assert.That(() => ShapeResolver.Resolve(
                    typeof(System.Collections.Generic.HashSet<int>),
                    new HashSet<System.Type>()))
                .ThrowsExactly<System.InvalidOperationException>();
            await Assert.That(ex.Message).Contains("is not supported");
        }

        [Test]
        public async Task Resolve_Queue_Throws()
        {
            var ex = await Assert.That(() => ShapeResolver.Resolve(
                    typeof(System.Collections.Generic.Queue<int>),
                    new HashSet<System.Type>()))
                .ThrowsExactly<System.InvalidOperationException>();
            await Assert.That(ex.Message).Contains("is not supported");
        }

        [Test]
        public async Task Resolve_IEnumerable_Throws()
        {
            var ex = await Assert.That(() => ShapeResolver.Resolve(
                    typeof(System.Collections.Generic.IEnumerable<int>),
                    new HashSet<System.Type>()))
                .ThrowsExactly<System.InvalidOperationException>();
            await Assert.That(ex.Message).Contains("is not supported");
        }

        [Test]
        public async Task Resolve_IList_Throws()
        {
            var ex = await Assert.That(() => ShapeResolver.Resolve(
                    typeof(System.Collections.Generic.IList<int>),
                    new HashSet<System.Type>()))
                .ThrowsExactly<System.InvalidOperationException>();
            await Assert.That(ex.Message).Contains("is not supported");
        }

        [Test]
        public async Task Resolve_Object_Throws()
        {
            var ex = await Assert.That(() => ShapeResolver.Resolve(typeof(object), new HashSet<System.Type>()))
                .ThrowsExactly<System.InvalidOperationException>();
            await Assert.That(ex.Message).Contains("is not supported");
        }
    }
}

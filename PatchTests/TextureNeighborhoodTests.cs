using System.Linq;
using Fuse;
using Fuse.compute;
using NUnit.Framework;
using Stride.Core.Mathematics;

namespace PatchTests;

[TestFixture]
[Category("FuseComputeCore")]
public class TextureNeighborhoodTests
{
    [Test]
    public void AxisOffsets_ReturnsDimensionalDirectNeighbors()
    {
        Assert.That(
            TextureNeighborhood.AxisOffsets(1).Select(offset => offset.ToShaderOffset(1)),
            Is.EqualTo(new[] { "-1", "1" }));
        Assert.That(
            TextureNeighborhood.AxisOffsets(2).Select(offset => offset.ToShaderOffset(2)),
            Is.EqualTo(new[] { "int2(-1, 0)", "int2(1, 0)", "int2(0, -1)", "int2(0, 1)" }));
        Assert.That(
            TextureNeighborhood.AxisOffsets(3).Select(offset => offset.ToShaderOffset(3)),
            Is.EqualTo(new[]
            {
                "int3(-1, 0, 0)",
                "int3(1, 0, 0)",
                "int3(0, -1, 0)",
                "int3(0, 1, 0)",
                "int3(0, 0, -1)",
                "int3(0, 0, 1)"
            }));
    }

    [Test]
    public void DiagonalOffsets_ReturnsMooreNeighborsBeyondAxisOffsets()
    {
        Assert.That(TextureNeighborhood.DiagonalOffsets(1), Is.Empty);
        Assert.That(
            TextureNeighborhood.DiagonalOffsets(2).Select(offset => offset.ToShaderOffset(2)),
            Is.EqualTo(new[]
            {
                "int2(-1, -1)",
                "int2(1, -1)",
                "int2(-1, 1)",
                "int2(1, 1)"
            }));
        Assert.That(TextureNeighborhood.DiagonalOffsets(3), Has.Count.EqualTo(20));
    }

    [Test]
    public void GetIndexDimension_MapsSupportedTextureIndexTypes()
    {
        Assert.That(TextureNeighborhood.GetIndexDimension<int>(), Is.EqualTo(1));
        Assert.That(TextureNeighborhood.GetIndexDimension<Int2>(), Is.EqualTo(2));
        Assert.That(TextureNeighborhood.GetIndexDimension<Int3>(), Is.EqualTo(3));
        Assert.That(TextureNeighborhood.GetIndexDimension<float>(), Is.EqualTo(0));
    }

    [Test]
    public void NeighborhoodNode_EmitsNestedRadiusLoopForIndexDimension()
    {
        var node = new TestNeighborhoodNode();

        var shaderCode = node.EmitRadiusLoop();

        Assert.That(shaderCode, Does.Contain("for (int testY_${resultName} = -radius_${resultName}"));
        Assert.That(shaderCode, Does.Contain("for (int testX_${resultName} = -radius_${resultName}"));
        Assert.That(shaderCode, Does.Contain("sample(int2(testX_${resultName}, testY_${resultName}))"));
    }

    [Test]
    public void NeighborhoodNode_EmitsOffsetBufferLoopForRuntimeOffsets()
    {
        var node = new TestNeighborhoodNode();

        var shaderCode = node.EmitOffsetBufferLoop();

        Assert.That(shaderCode, Does.Contain("for (int bufferOffsetIndex_${resultName} = 0; bufferOffsetIndex_${resultName} < OffsetCount; bufferOffsetIndex_${resultName}++)"));
        Assert.That(shaderCode, Does.Contain("int2 bufferOffset_${resultName} = Offsets[bufferOffsetIndex_${resultName}];"));
        Assert.That(shaderCode, Does.Contain("sample(bufferOffset_${resultName})"));
    }

    private sealed class TestNeighborhoodNode : TextureNeighborhoodNode<Int2, float>
    {
        public TestNeighborhoodNode()
            : base(
                null,
                "TestNeighborhood",
                null,
                new ShaderNode<Int2>(null, "Index", theCreateDefault: false))
        {
            RefreshInputs();
        }

        public string EmitRadiusLoop()
        {
            return EmitNestedRadiusLoop(
                "test",
                "radius_${resultName}",
                "sum += sample(${offset});");
        }

        public string EmitOffsetBufferLoop()
        {
            return EmitOffsetBufferLoop(
                "buffer",
                "Offsets",
                "OffsetCount",
                "sum += sample(${offset});");
        }

        protected override string SourceTemplate()
        {
            return "";
        }
    }
}

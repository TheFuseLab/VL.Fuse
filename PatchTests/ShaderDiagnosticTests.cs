using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Fuse;
using NUnit.Framework;

namespace PatchTests;

[TestFixture]
public class ShaderDiagnosticTests
{
    [Test]
    public void PropertiesForTree_ContinuesAfterNonMatchingPropertyKey()
    {
        var node = CreateTestShaderNode();
        node.SetProperty("Number", 123);
        node.SetProperty("Text", "expected");

        var result = node.PropertiesForTree<string>();

        Assert.That(result.Keys, Does.Contain("Text"));
        Assert.That(result["Text"], Is.EqualTo(new[] { "expected" }));
    }

    [Test]
    public void OrderedUniqueCollection_PreservesFirstSeenOrder()
    {
        var values = new OrderedUniqueCollection<string>();

        values.Add("b");
        values.Add("a");
        values.Add("b");
        values.Add("c");

        Assert.That(values, Is.EqualTo(new[] { "b", "a", "c" }));
        Assert.That(values.Count, Is.EqualTo(3));
    }

    [Test]
    public void ShaderSourceRegistrationCache_IsScopedToSourceManagerAndSourceText()
    {
        var sourceManagerA = new object();
        var sourceManagerB = new object();

        Assert.That(IsShaderSourceRegistered(sourceManagerA, "Shader_1", "code A", "shaders\\Shader_1.sdsl"), Is.False);

        MarkShaderSourceRegistered(sourceManagerA, "Shader_1", "code A", "shaders\\Shader_1.sdsl");

        Assert.That(IsShaderSourceRegistered(sourceManagerA, "Shader_1", "code A", "shaders\\Shader_1.sdsl"), Is.True);
        Assert.That(IsShaderSourceRegistered(sourceManagerA, "Shader_1", "code B", "shaders\\Shader_1.sdsl"), Is.False);
        Assert.That(IsShaderSourceRegistered(sourceManagerB, "Shader_1", "code A", "shaders\\Shader_1.sdsl"), Is.False);
    }

    [Test]
    public void ShaderDiagnosticContext_Create_CapturesStageDeclarationsAndWarnings()
    {
        var compilation = new ShaderCompilationResult();
        compilation.Mixins.Add("FuseMath");
        compilation.Mixins.Add("FuseMath");
        compilation.Declarations.Add(new FieldDeclaration(false, "float", "float", "inputA"));
        compilation.Declarations.Add(new FieldDeclaration(true, "RWStructuredBuffer<float4>", "StructuredBuffer<float4>", "bufferA"));

        var diagnostics = ShaderDiagnosticContext.Create(
            "Shader_123",
            "compute",
            "shaders\\Shader_123.sdsl",
            true,
            "shader Shader_123 {}",
            new[]
            {
                new ShaderStageCompilationDiagnostic(
                    new ShaderStageDiagnostic("FX", "assign_1", "Fuse.AssignValue"),
                    compilation)
            },
            new[] { "Generated shader failed validation: dangling placeholder" },
            new[] { new ShaderTimingDiagnostic("Stage:FX:CompileProperties", 1.25) });

        Assert.That(diagnostics.IsCompute, Is.True);
        Assert.That(diagnostics.Phase, Is.EqualTo("compute"));
        Assert.That(diagnostics.CodeLength, Is.GreaterThan(0));
        Assert.That(diagnostics.Mixins, Is.EqualTo(new[] { "FuseMath" }));
        Assert.That(diagnostics.Stages.Count, Is.EqualTo(1));
        Assert.That(diagnostics.Stages[0].Key, Is.EqualTo("FX"));
        Assert.That(diagnostics.Declarations.Select(d => d.InputName), Is.EqualTo(new[] { "bufferA", "inputA" }));
        Assert.That(diagnostics.Declarations.Single(d => d.InputName == "bufferA").IsResource, Is.True);
        Assert.That(diagnostics.Timings.Single().Name, Is.EqualTo("Stage:FX:CompileProperties"));
        Assert.That(diagnostics.Warnings.Single(), Does.Contain("dangling placeholder"));

        var log = diagnostics.ToDiagnosticLog();
        Assert.That(log, Does.Contain("Shader: Shader_123"));
        Assert.That(log, Does.Contain("Phase: compute"));
        Assert.That(log, Does.Contain("Name=bufferA; Resource=True"));
        Assert.That(log, Does.Contain("Stage:FX:CompileProperties: 1.25 ms"));
    }

    [Test]
    public void DumpShaderDiagnostics_Force_WritesDiagnosticLog()
    {
        var previousTrace = ShaderNodesUtil.TraceShaderSource;
        var previousDirectory = ShaderNodesUtil.ShaderDumpDirectory;
        var dumpDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "shader-diagnostics-test");
        Directory.CreateDirectory(dumpDirectory);

        try
        {
            ShaderNodesUtil.TraceShaderSource = false;
            ShaderNodesUtil.ShaderDumpDirectory = dumpDirectory;
            foreach (var file in Directory.GetFiles(dumpDirectory, "*_Shader_456_compute_diagnostics.log"))
                File.Delete(file);

            var diagnostics = ShaderDiagnosticContext.Create(
                "Shader_456",
                "compute",
                "shaders\\Shader_456.sdsl",
                true,
                "shader Shader_456 {}",
                new List<ShaderStageCompilationDiagnostic>(),
                new[] { "forced dump" });

            ShaderNodesUtil.DumpShaderDiagnostics(diagnostics, force: true);

            var files = Directory.GetFiles(dumpDirectory, "*_Shader_456_compute_diagnostics.log");
            Assert.That(files.Length, Is.EqualTo(1));
            Assert.That(File.ReadAllText(files[0]), Does.Contain("forced dump"));
        }
        finally
        {
            ShaderNodesUtil.TraceShaderSource = previousTrace;
            ShaderNodesUtil.ShaderDumpDirectory = previousDirectory;
        }
    }

    [Test]
    public void PatchTests_ExternalCascadeClassifier_OnlyMatchesStrideCascadeErrors()
    {
        Assert.That(
            Fuse.Tests.PatchTests.IsKnownExternalPackageCascadeMessageText(
                "BoxFrameRenderer [Stride.Models.Meshes - Advanced] - Advanced has errors."),
            Is.True);
        Assert.That(
            Fuse.Tests.PatchTests.IsKnownExternalPackageCascadeMessageText(
                "Blend [Stride.Textures.Mixer] has errors."),
            Is.True);
        Assert.That(
            Fuse.Tests.PatchTests.IsKnownExternalPackageCascadeMessageText(
                "SomeFuseNode [Fuse.Compute] has errors."),
            Is.False);
        Assert.That(
            Fuse.Tests.PatchTests.IsKnownExternalPackageCascadeMessageText(
                "BlendMixer doesn't have a pin called \"Input\"."),
            Is.False);
    }

    private sealed class TestShaderNode : AbstractShaderNode
    {
        private TestShaderNode()
            : base(null, "Test")
        {
        }

        public override AbstractShaderNode AbstractDefault => null;

        public override string ID => "Test";

        public override string TypeName() => "float";

        public override int Dimension() => 1;

        protected override string SourceTemplate() => "";
    }

    private static TestShaderNode CreateTestShaderNode()
    {
        var node = (TestShaderNode)RuntimeHelpers.GetUninitializedObject(typeof(TestShaderNode));
        typeof(AbstractShaderNode)
            .GetField("<Property>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(node, new Dictionary<string, IList>());
        node.Ins = [];
        return node;
    }

    private static bool IsShaderSourceRegistered(
        object sourceManager,
        string type,
        string sourceCode,
        string sourcePath)
    {
        var method = typeof(ShaderNodesUtil).GetMethod(
            "IsShaderSourceRegistered",
            BindingFlags.Static | BindingFlags.NonPublic);

        return (bool)method.Invoke(null, new[] { sourceManager, type, sourceCode, sourcePath });
    }

    private static void MarkShaderSourceRegistered(
        object sourceManager,
        string type,
        string sourceCode,
        string sourcePath)
    {
        var method = typeof(ShaderNodesUtil).GetMethod(
            "MarkShaderSourceRegistered",
            BindingFlags.Static | BindingFlags.NonPublic);

        method.Invoke(null, new[] { sourceManager, type, sourceCode, sourcePath });
    }
}

using System;
using System.IO;
using System.Linq;
using Fuse;
using Fuse.MixinNodeFactory;
using Fuse.function;
using NUnit.Framework;
using Stride.Core.Mathematics;

namespace PatchTests;

[TestFixture]
public class MixinNodeFactoryTests
{
    private string _testShaderPath;
    private string _testShaderContent;

    [SetUp]
    public void Setup()
    {
        // Find the test shader file
        var baseDir = TestContext.CurrentContext.TestDirectory;
        _testShaderPath = Path.Combine(baseDir, "..", "..", "..", "..", "vl", "shaders", "FuseExportedMixin.sdsl");

        if (!File.Exists(_testShaderPath))
        {
            // Try alternate path
            _testShaderPath = @"D:\development\vl\repo\VL.Fuse\vl\shaders\FuseExportedMixin.sdsl";
        }

        if (File.Exists(_testShaderPath))
        {
            _testShaderContent = File.ReadAllText(_testShaderPath);
        }
    }

    #region MixinCommentParser Tests

    [Test]
    public void CommentParser_ParsesExportDirective()
    {
        var parser = new MixinCommentParser();
        var comments = @"// @export
// @namespace Fuse.Math
// @summary Test function";

        var metadata = parser.ParseComments(comments);

        Assert.That(metadata.IsExported, Is.True);
    }

    [Test]
    public void CommentParser_ParsesNamespace()
    {
        var parser = new MixinCommentParser();
        var comments = @"// @export
// @namespace Fuse.Math.Special
// @summary Test function";

        var metadata = parser.ParseComments(comments);

        Assert.That(metadata.Namespace, Is.EqualTo("Fuse.Math.Special"));
    }

    [Test]
    public void CommentParser_ParsesSummary()
    {
        var parser = new MixinCommentParser();
        var comments = @"// @export
// @summary This is a test summary";

        var metadata = parser.ParseComments(comments);

        Assert.That(metadata.Summary, Is.EqualTo("This is a test summary"));
    }

    [Test]
    public void CommentParser_ParsesParamDescriptions()
    {
        var parser = new MixinCommentParser();
        var comments = @"// @export
// @param a First parameter
// @param b Second parameter";

        var metadata = parser.ParseComments(comments);

        Assert.That(metadata.ParamDescriptions.Count, Is.EqualTo(2));
        Assert.That(metadata.ParamDescriptions["a"], Is.EqualTo("First parameter"));
        Assert.That(metadata.ParamDescriptions["b"], Is.EqualTo("Second parameter"));
    }

    [Test]
    public void CommentParser_ParsesDefaultValues()
    {
        var parser = new MixinCommentParser();
        var comments = @"// @export
// @default radius 1.0
// @default intensity 0.5";

        var metadata = parser.ParseComments(comments);

        Assert.That(metadata.ParamDefaults.Count, Is.EqualTo(2));
        Assert.That(metadata.ParamDefaults["radius"], Is.EqualTo("1.0"));
        Assert.That(metadata.ParamDefaults["intensity"], Is.EqualTo("0.5"));
    }

    [Test]
    public void CommentParser_ParsesGroupable()
    {
        var parser = new MixinCommentParser();
        var comments = @"// @export
// @groupable
// @groupoptions 2";

        var metadata = parser.ParseComments(comments);

        Assert.That(metadata.IsGroupable, Is.True);
        Assert.That(metadata.GroupOptions, Is.EqualTo(2));
    }

    [Test]
    public void CommentParser_HandlesBlockComments()
    {
        var parser = new MixinCommentParser();
        var comments = @"/*
 * @export
 * @namespace Fuse.Test
 * @summary Block comment test
 */";

        var metadata = parser.ParseComments(comments);

        Assert.That(metadata.IsExported, Is.True);
        Assert.That(metadata.Namespace, Is.EqualTo("Fuse.Test"));
    }

    [Test]
    public void CommentParser_NonExportedReturnsNotExported()
    {
        var parser = new MixinCommentParser();
        var comments = @"// This is just a regular comment
// No export directive here";

        var metadata = parser.ParseComments(comments);

        Assert.That(metadata.IsExported, Is.False);
    }

    #endregion

    #region SdslTypeMapper Tests

    [Test]
    public void TypeMapper_MapsFloatTypes()
    {
        Assert.That(SdslTypeMapper.GetClrType("float"), Is.EqualTo(typeof(float)));
        Assert.That(SdslTypeMapper.GetClrType("float2"), Is.EqualTo(typeof(Vector2)));
        Assert.That(SdslTypeMapper.GetClrType("float3"), Is.EqualTo(typeof(Vector3)));
        Assert.That(SdslTypeMapper.GetClrType("float4"), Is.EqualTo(typeof(Vector4)));
    }

    [Test]
    public void TypeMapper_MapsIntTypes()
    {
        Assert.That(SdslTypeMapper.GetClrType("int"), Is.EqualTo(typeof(int)));
        Assert.That(SdslTypeMapper.GetClrType("int2"), Is.EqualTo(typeof(Int2)));
        Assert.That(SdslTypeMapper.GetClrType("int3"), Is.EqualTo(typeof(Int3)));
        Assert.That(SdslTypeMapper.GetClrType("int4"), Is.EqualTo(typeof(Int4)));
        Assert.That(SdslTypeMapper.GetClrType("uint"), Is.EqualTo(typeof(uint)));
    }

    [Test]
    public void TypeMapper_MapsBool()
    {
        Assert.That(SdslTypeMapper.GetClrType("bool"), Is.EqualTo(typeof(bool)));
    }

    [Test]
    public void TypeMapper_MapsMatrixTypes()
    {
        Assert.That(SdslTypeMapper.GetClrType("float4x4"), Is.EqualTo(typeof(Matrix)));
        Assert.That(SdslTypeMapper.GetClrType("float3x3"), Is.EqualTo(typeof(Matrix3)));
    }

    [Test]
    public void TypeMapper_MapsVoid()
    {
        Assert.That(SdslTypeMapper.GetClrType("void"), Is.EqualTo(typeof(Fuse.compute.GpuVoid)));
    }

    [Test]
    public void TypeMapper_ParsesFloatValue()
    {
        var value = SdslTypeMapper.ParseValue("float", "1.5");
        Assert.That(value, Is.EqualTo(1.5f));
    }

    [Test]
    public void TypeMapper_ParsesVector3Value()
    {
        var value = SdslTypeMapper.ParseValue("float3", "float3(1.0, 2.0, 3.0)");
        Assert.That(value, Is.EqualTo(new Vector3(1f, 2f, 3f)));
    }

    [Test]
    public void TypeMapper_ParsesBoolValue()
    {
        Assert.That(SdslTypeMapper.ParseValue("bool", "true"), Is.EqualTo(true));
        Assert.That(SdslTypeMapper.ParseValue("bool", "false"), Is.EqualTo(false));
    }

    [Test]
    public void TypeMapper_ReturnsNullForUnknownType()
    {
        Assert.That(SdslTypeMapper.GetClrType("unknowntype"), Is.Null);
    }

    #endregion

    #region MixinFunctionParser Tests

    [Test]
    public void FunctionParser_ParsesSimpleFunction()
    {
        var parser = new MixinFunctionParser();
        var content = @"shader TestMixin
{
    // @export
    // @namespace Fuse.Test
    // @summary Simple test function
    float testFunc(float a, float b)
    {
        return a + b;
    }
}";

        var functions = parser.ParseContent(content, "test.sdsl");

        Assert.That(functions.Count, Is.EqualTo(1));
        Assert.That(functions[0].Name, Is.EqualTo("testFunc"));
        Assert.That(functions[0].ReturnType, Is.EqualTo("float"));
        Assert.That(functions[0].MixinName, Is.EqualTo("TestMixin"));
        Assert.That(functions[0].Parameters.Count, Is.EqualTo(2));
    }

    [Test]
    public void FunctionParser_ParsesOutParameter()
    {
        var parser = new MixinFunctionParser();
        var content = @"shader TestMixin
{
    // @export
    float testFunc(float a, out float result)
    {
        result = a * 2;
        return a;
    }
}";

        var functions = parser.ParseContent(content, "test.sdsl");

        Assert.That(functions.Count, Is.EqualTo(1));
        Assert.That(functions[0].Parameters.Count, Is.EqualTo(2));
        Assert.That(functions[0].Parameters[0].Modifier, Is.EqualTo(InputModifier.In));
        Assert.That(functions[0].Parameters[1].Modifier, Is.EqualTo(InputModifier.Out));
    }

    [Test]
    public void FunctionParser_ParsesInOutParameter()
    {
        var parser = new MixinFunctionParser();
        var content = @"shader TestMixin
{
    // @export
    void testFunc(inout float value)
    {
        value = value * 2;
    }
}";

        var functions = parser.ParseContent(content, "test.sdsl");

        Assert.That(functions.Count, Is.EqualTo(1));
        Assert.That(functions[0].Parameters[0].Modifier, Is.EqualTo(InputModifier.InOut));
    }

    [Test]
    public void FunctionParser_ParsesDefaultValue()
    {
        var parser = new MixinFunctionParser();
        var content = @"shader TestMixin
{
    // @export
    float testFunc(float a, float b = 1.0)
    {
        return a + b;
    }
}";

        var functions = parser.ParseContent(content, "test.sdsl");

        Assert.That(functions[0].Parameters[1].HasInlineDefault, Is.True);
        Assert.That(functions[0].Parameters[1].DefaultValue, Is.EqualTo(1.0f));
    }

    [Test]
    public void FunctionParser_ParsesVectorParameters()
    {
        var parser = new MixinFunctionParser();
        var content = @"shader TestMixin
{
    // @export
    float3 testFunc(float3 position, float2 uv)
    {
        return position;
    }
}";

        var functions = parser.ParseContent(content, "test.sdsl");

        Assert.That(functions[0].ReturnType, Is.EqualTo("float3"));
        Assert.That(functions[0].ClrReturnType, Is.EqualTo(typeof(Vector3)));
        Assert.That(functions[0].Parameters[0].SdslType, Is.EqualTo("float3"));
        Assert.That(functions[0].Parameters[0].ClrType, Is.EqualTo(typeof(Vector3)));
        Assert.That(functions[0].Parameters[1].SdslType, Is.EqualTo("float2"));
        Assert.That(functions[0].Parameters[1].ClrType, Is.EqualTo(typeof(Vector2)));
    }

    [Test]
    public void FunctionParser_IgnoresNonExportedFunctions()
    {
        var parser = new MixinFunctionParser();
        var content = @"shader TestMixin
{
    // This function is not exported
    float helperFunc(float a)
    {
        return a * 2;
    }

    // @export
    float exportedFunc(float a)
    {
        return helperFunc(a);
    }
}";

        var functions = parser.ParseContent(content, "test.sdsl");

        Assert.That(functions.Count, Is.EqualTo(1));
        Assert.That(functions[0].Name, Is.EqualTo("exportedFunc"));
    }

    [Test]
    public void FunctionParser_ParsesMultipleFunctions()
    {
        var parser = new MixinFunctionParser();
        var content = @"shader TestMixin
{
    // @export
    // @namespace Fuse.A
    float funcA(float a) { return a; }

    // @export
    // @namespace Fuse.B
    float funcB(float b) { return b; }

    // @export
    // @namespace Fuse.C
    float funcC(float c) { return c; }
}";

        var functions = parser.ParseContent(content, "test.sdsl");

        Assert.That(functions.Count, Is.EqualTo(3));
        Assert.That(functions[0].Name, Is.EqualTo("funcA"));
        Assert.That(functions[1].Name, Is.EqualTo("funcB"));
        Assert.That(functions[2].Name, Is.EqualTo("funcC"));
    }

    [Test]
    public void FunctionParser_ExtractsMixinName()
    {
        var parser = new MixinFunctionParser();
        var content = @"shader MyCustomMixin : BaseMixin
{
    // @export
    float test(float a) { return a; }
}";

        var mixinName = parser.GetMixinName(content);

        Assert.That(mixinName, Is.EqualTo("MyCustomMixin"));
    }

    [Test]
    public void FunctionParser_ExtractsBaseShaders()
    {
        var parser = new MixinFunctionParser();
        var content = @"shader MyMixin : Base1, Base2, Base3
{
}";

        var baseShaders = parser.GetBaseShaders(content);

        Assert.That(baseShaders.Count, Is.EqualTo(3));
        Assert.That(baseShaders, Contains.Item("Base1"));
        Assert.That(baseShaders, Contains.Item("Base2"));
        Assert.That(baseShaders, Contains.Item("Base3"));
    }

    #endregion

    #region Integration Tests with Example File

    [Test]
    public void Integration_ParsesExampleShaderFile()
    {
        if (!File.Exists(_testShaderPath))
        {
            Assert.Ignore("Example shader file not found");
            return;
        }

        var parser = new MixinFunctionParser();
        var functions = parser.ParseFile(_testShaderPath);

        Assert.That(functions.Count, Is.GreaterThan(0), "Should find exported functions");

        // Log all found functions
        TestContext.WriteLine($"Found {functions.Count} exported functions:");
        foreach (var func in functions)
        {
            TestContext.WriteLine($"  - {func.Name} ({func.ReturnType}) in {func.Metadata.Namespace}");
        }
    }

    [Test]
    public void Integration_FindsPowsFunction()
    {
        if (!File.Exists(_testShaderPath))
        {
            Assert.Ignore("Example shader file not found");
            return;
        }

        var parser = new MixinFunctionParser();
        var functions = parser.ParseFile(_testShaderPath);

        var pows = functions.FirstOrDefault(f => f.Name == "pows");

        Assert.That(pows, Is.Not.Null, "Should find pows function");
        Assert.That(pows.ReturnType, Is.EqualTo("float"));
        Assert.That(pows.Metadata.Namespace, Is.EqualTo("Fuse.Math"));
        Assert.That(pows.Parameters.Count, Is.EqualTo(2));
        Assert.That(pows.Parameters[0].Name, Is.EqualTo("a"));
        Assert.That(pows.Parameters[1].Name, Is.EqualTo("b"));
    }

    [Test]
    public void Integration_FindsSDFFunctions()
    {
        if (!File.Exists(_testShaderPath))
        {
            Assert.Ignore("Example shader file not found");
            return;
        }

        var parser = new MixinFunctionParser();
        var functions = parser.ParseFile(_testShaderPath);

        var sdfFunctions = functions.Where(f => f.Metadata.Namespace.StartsWith("Fuse.SDF")).ToList();

        Assert.That(sdfFunctions.Count, Is.GreaterThan(0), "Should find SDF functions");

        // Check for specific SDF functions
        Assert.That(sdfFunctions.Any(f => f.Name == "fSphere"), Is.True, "Should have fSphere");
        Assert.That(sdfFunctions.Any(f => f.Name == "fBox"), Is.True, "Should have fBox");
        Assert.That(sdfFunctions.Any(f => f.Name == "fTorus"), Is.True, "Should have fTorus");
    }

    [Test]
    public void Integration_FindsFunctionWithOutParameter()
    {
        if (!File.Exists(_testShaderPath))
        {
            Assert.Ignore("Example shader file not found");
            return;
        }

        var parser = new MixinFunctionParser();
        var functions = parser.ParseFile(_testShaderPath);

        var funcWithOut = functions.FirstOrDefault(f => f.Name == "fOpUnionSmooth");

        Assert.That(funcWithOut, Is.Not.Null, "Should find fOpUnionSmooth");

        var outParams = funcWithOut.Parameters.Where(p => p.Modifier == InputModifier.Out).ToList();
        Assert.That(outParams.Count, Is.EqualTo(1), "Should have one out parameter");
        Assert.That(outParams[0].Name, Is.EqualTo("blend"));
    }

    [Test]
    public void Integration_FindsGroupableFunctions()
    {
        if (!File.Exists(_testShaderPath))
        {
            Assert.Ignore("Example shader file not found");
            return;
        }

        var parser = new MixinFunctionParser();
        var functions = parser.ParseFile(_testShaderPath);

        var groupable = functions.Where(f => f.Metadata.IsGroupable).ToList();

        Assert.That(groupable.Count, Is.GreaterThan(0), "Should find groupable functions");
        Assert.That(groupable.Any(f => f.Name == "fOpUnion"), Is.True, "fOpUnion should be groupable");
        Assert.That(groupable.Any(f => f.Name == "fOpIntersection"), Is.True, "fOpIntersection should be groupable");
    }

    [Test]
    public void Integration_CorrectNamespaceDistribution()
    {
        if (!File.Exists(_testShaderPath))
        {
            Assert.Ignore("Example shader file not found");
            return;
        }

        var parser = new MixinFunctionParser();
        var functions = parser.ParseFile(_testShaderPath);

        var byNamespace = functions.GroupBy(f => f.Metadata.Namespace).ToDictionary(g => g.Key, g => g.Count());

        TestContext.WriteLine("Functions by namespace:");
        foreach (var ns in byNamespace.OrderBy(kv => kv.Key))
        {
            TestContext.WriteLine($"  {ns.Key}: {ns.Value}");
        }

        Assert.That(byNamespace.ContainsKey("Fuse.Math"), Is.True);
        Assert.That(byNamespace.ContainsKey("Fuse.SDF.3D"), Is.True);
        Assert.That(byNamespace.ContainsKey("Fuse.SDF.Combine"), Is.True);
    }

    #endregion

    #region MixinInputFactory Tests

    [Test]
    public void InputFactory_ReturnsCorrectInputInfo()
    {
        var funcInfo = new MixinFunctionInfo
        {
            Name = "test",
            ReturnType = "float",
            ClrReturnType = typeof(float),
            Parameters = new System.Collections.Generic.List<MixinParameterInfo>
            {
                new() { Name = "a", SdslType = "float", ClrType = typeof(float), Modifier = InputModifier.In },
                new() { Name = "b", SdslType = "float3", ClrType = typeof(Vector3), Modifier = InputModifier.In },
                new() { Name = "result", SdslType = "float", ClrType = typeof(float), Modifier = InputModifier.Out }
            }
        };

        var inputs = MixinInputFactory.GetInputOnlyInfo(funcInfo);
        var outputs = MixinInputFactory.GetOutputOnlyInfo(funcInfo);

        Assert.That(inputs.Count, Is.EqualTo(2));
        Assert.That(outputs.Count, Is.EqualTo(1));
        Assert.That(outputs[0].Name, Is.EqualTo("result"));
    }

    [Test]
    public void InputFactory_GetAllOutputsIncludesReturn()
    {
        var funcInfo = new MixinFunctionInfo
        {
            Name = "test",
            ReturnType = "float3",
            ClrReturnType = typeof(Vector3),
            Parameters = new System.Collections.Generic.List<MixinParameterInfo>
            {
                new() { Name = "a", SdslType = "float", ClrType = typeof(float), Modifier = InputModifier.In },
                new() { Name = "extra", SdslType = "float", ClrType = typeof(float), Modifier = InputModifier.Out }
            }
        };

        var allOutputs = MixinInputFactory.GetAllOutputInfo(funcInfo);

        Assert.That(allOutputs.Count, Is.EqualTo(2));
        Assert.That(allOutputs[0].Name, Is.EqualTo("Result"));
        Assert.That(allOutputs[0].ClrType, Is.EqualTo(typeof(Vector3)));
        Assert.That(allOutputs[1].Name, Is.EqualTo("extra"));
    }

    #endregion

    #region MixinNodeFactory Tests

    [Test]
    public void Factory_ScanDirectory_FindsExportedFunctions()
    {
        var shaderDir = Path.GetDirectoryName(_testShaderPath);
        if (!Directory.Exists(shaderDir))
        {
            Assert.Ignore("Shader directory not found");
            return;
        }

        var functions = Fuse.MixinNodeFactory.MixinNodeFactory.ScanDirectory(shaderDir);

        Assert.That(functions.Count, Is.GreaterThan(0), "Should find exported functions in directory");
    }

    [Test]
    public void Factory_ScanDirectory_ReturnsEmptyForNonExistentDirectory()
    {
        var functions = Fuse.MixinNodeFactory.MixinNodeFactory.ScanDirectory(@"C:\NonExistent\Path\12345");

        Assert.That(functions.Count, Is.EqualTo(0));
    }

    [Test]
    public void Factory_ScanDirectory_NonRecursive()
    {
        var shaderDir = Path.GetDirectoryName(_testShaderPath);
        if (!Directory.Exists(shaderDir))
        {
            Assert.Ignore("Shader directory not found");
            return;
        }

        var functionsRecursive = Fuse.MixinNodeFactory.MixinNodeFactory.ScanDirectory(shaderDir, recursive: true);
        var functionsNonRecursive = Fuse.MixinNodeFactory.MixinNodeFactory.ScanDirectory(shaderDir, recursive: false);

        // Non-recursive should find same or fewer functions
        Assert.That(functionsNonRecursive.Count, Is.LessThanOrEqualTo(functionsRecursive.Count));
    }

    [Test]
    public void Factory_ParseFile_ReturnsExportedFunctions()
    {
        if (!File.Exists(_testShaderPath))
        {
            Assert.Ignore("Example shader file not found");
            return;
        }

        var functions = Fuse.MixinNodeFactory.MixinNodeFactory.ParseFile(_testShaderPath);

        Assert.That(functions.Count, Is.GreaterThan(0));
        Assert.That(functions.Any(f => f.Name == "pows"), Is.True);
    }

    [Test]
    public void Factory_GetCategory_ReturnsNamespaceWhenSet()
    {
        var funcInfo = new MixinFunctionInfo
        {
            Name = "test",
            MixinName = "TestMixin",
            Metadata = new MixinMetadata { Namespace = "Fuse.Custom.Category" }
        };

        var category = Fuse.MixinNodeFactory.MixinNodeFactory.GetCategory(funcInfo);

        Assert.That(category, Is.EqualTo("Fuse.Custom.Category"));
    }

    [Test]
    public void Factory_GetCategory_ReturnsMixinBasedCategoryWhenNoNamespace()
    {
        var funcInfo = new MixinFunctionInfo
        {
            Name = "test",
            MixinName = "MyMixin",
            Metadata = new MixinMetadata { Namespace = null }
        };

        var category = Fuse.MixinNodeFactory.MixinNodeFactory.GetCategory(funcInfo);

        Assert.That(category, Is.EqualTo("Fuse.Mixin.MyMixin"));
    }

    [Test]
    public void Factory_GetCategory_HandlesEmptyNamespace()
    {
        var funcInfo = new MixinFunctionInfo
        {
            Name = "test",
            MixinName = "AnotherMixin",
            Metadata = new MixinMetadata { Namespace = "" }
        };

        var category = Fuse.MixinNodeFactory.MixinNodeFactory.GetCategory(funcInfo);

        Assert.That(category, Is.EqualTo("Fuse.Mixin.AnotherMixin"));
    }

    [Test]
    public void Factory_GetCategories_ReturnsUniqueCategories()
    {
        var functions = new[]
        {
            new MixinFunctionInfo { Name = "a", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.Math" } },
            new MixinFunctionInfo { Name = "b", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.Math" } },
            new MixinFunctionInfo { Name = "c", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.SDF" } },
            new MixinFunctionInfo { Name = "d", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.Noise" } },
        };

        var categories = Fuse.MixinNodeFactory.MixinNodeFactory.GetCategories(functions).ToList();

        Assert.That(categories.Count, Is.EqualTo(3));
        Assert.That(categories, Contains.Item("Fuse.Math"));
        Assert.That(categories, Contains.Item("Fuse.SDF"));
        Assert.That(categories, Contains.Item("Fuse.Noise"));
    }

    [Test]
    public void Factory_GetCategories_SortsAlphabetically()
    {
        var functions = new[]
        {
            new MixinFunctionInfo { Name = "a", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.Z" } },
            new MixinFunctionInfo { Name = "b", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.A" } },
            new MixinFunctionInfo { Name = "c", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.M" } },
        };

        var categories = Fuse.MixinNodeFactory.MixinNodeFactory.GetCategories(functions).ToList();

        Assert.That(categories[0], Is.EqualTo("Fuse.A"));
        Assert.That(categories[1], Is.EqualTo("Fuse.M"));
        Assert.That(categories[2], Is.EqualTo("Fuse.Z"));
    }

    [Test]
    public void Factory_GroupByCategory_GroupsFunctionsCorrectly()
    {
        var functions = new[]
        {
            new MixinFunctionInfo { Name = "pows", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.Math" } },
            new MixinFunctionInfo { Name = "smin", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.Math" } },
            new MixinFunctionInfo { Name = "fSphere", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.SDF" } },
            new MixinFunctionInfo { Name = "noise", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.Noise" } },
        };

        var grouped = Fuse.MixinNodeFactory.MixinNodeFactory.GroupByCategory(functions);

        Assert.That(grouped.Count, Is.EqualTo(3));
        Assert.That(grouped["Fuse.Math"].Count, Is.EqualTo(2));
        Assert.That(grouped["Fuse.SDF"].Count, Is.EqualTo(1));
        Assert.That(grouped["Fuse.Noise"].Count, Is.EqualTo(1));
    }

    [Test]
    public void Factory_GroupByCategory_PreservesFunctionOrder()
    {
        var functions = new[]
        {
            new MixinFunctionInfo { Name = "first", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.Test" } },
            new MixinFunctionInfo { Name = "second", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.Test" } },
            new MixinFunctionInfo { Name = "third", MixinName = "M", Metadata = new MixinMetadata { Namespace = "Fuse.Test" } },
        };

        var grouped = Fuse.MixinNodeFactory.MixinNodeFactory.GroupByCategory(functions);

        Assert.That(grouped["Fuse.Test"][0].Name, Is.EqualTo("first"));
        Assert.That(grouped["Fuse.Test"][1].Name, Is.EqualTo("second"));
        Assert.That(grouped["Fuse.Test"][2].Name, Is.EqualTo("third"));
    }

    [Test]
    public void Factory_Integration_GroupsExampleFileCorrectly()
    {
        if (!File.Exists(_testShaderPath))
        {
            Assert.Ignore("Example shader file not found");
            return;
        }

        var functions = Fuse.MixinNodeFactory.MixinNodeFactory.ParseFile(_testShaderPath);
        var grouped = Fuse.MixinNodeFactory.MixinNodeFactory.GroupByCategory(functions);

        TestContext.WriteLine("Grouped functions from example file:");
        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            TestContext.WriteLine($"  {group.Key}:");
            foreach (var func in group.Value)
            {
                TestContext.WriteLine($"    - {func.Name}");
            }
        }

        // Verify expected categories exist
        Assert.That(grouped.ContainsKey("Fuse.Math"), Is.True);
        Assert.That(grouped.ContainsKey("Fuse.SDF.3D"), Is.True);
        Assert.That(grouped.ContainsKey("Fuse.SDF.Combine"), Is.True);
    }

    [Test]
    public void Factory_WatchDirectory_ReturnsEmptyForNonExistentDirectory()
    {
        var observable = Fuse.MixinNodeFactory.MixinNodeFactory.WatchDirectory(@"C:\NonExistent\Path\12345");

        // Should not throw and return empty observable
        Assert.That(observable, Is.Not.Null);
    }

    #endregion

    #region InOut Parameter Tests

    [Test]
    public void InOutParameter_CreatesInputAndOutputPins()
    {
        var funcInfo = new MixinFunctionInfo
        {
            Name = "testInOut",
            ReturnType = "void",
            ClrReturnType = typeof(Fuse.compute.GpuVoid),
            Parameters = new System.Collections.Generic.List<MixinParameterInfo>
            {
                new() { Name = "value", SdslType = "float", ClrType = typeof(float), Modifier = InputModifier.InOut }
            }
        };

        var inputs = MixinInputFactory.GetInputOnlyInfo(funcInfo);
        var outputs = MixinInputFactory.GetOutputOnlyInfo(funcInfo);

        // InOut should appear as both input AND output
        Assert.That(inputs.Count, Is.EqualTo(1), "InOut param should create input pin");
        Assert.That(outputs.Count, Is.EqualTo(1), "InOut param should create output pin");
        Assert.That(inputs[0].Name, Is.EqualTo("value"));
        Assert.That(outputs[0].Name, Is.EqualTo("value"));
    }

    [Test]
    public void InOutParameter_MixedWithOutParameter()
    {
        var funcInfo = new MixinFunctionInfo
        {
            Name = "testMixed",
            ReturnType = "float",
            ClrReturnType = typeof(float),
            Parameters = new System.Collections.Generic.List<MixinParameterInfo>
            {
                new() { Name = "a", SdslType = "float", ClrType = typeof(float), Modifier = InputModifier.In },
                new() { Name = "b", SdslType = "float3", ClrType = typeof(Vector3), Modifier = InputModifier.InOut },
                new() { Name = "c", SdslType = "float", ClrType = typeof(float), Modifier = InputModifier.Out }
            }
        };

        var inputs = MixinInputFactory.GetInputOnlyInfo(funcInfo);
        var outputs = MixinInputFactory.GetAllOutputInfo(funcInfo);

        // Inputs: a (In) + b (InOut) = 2
        Assert.That(inputs.Count, Is.EqualTo(2));
        Assert.That(inputs[0].Name, Is.EqualTo("a"));
        Assert.That(inputs[1].Name, Is.EqualTo("b"));

        // Outputs: Result (return) + b (InOut) + c (Out) = 3
        Assert.That(outputs.Count, Is.EqualTo(3));
        Assert.That(outputs[0].Name, Is.EqualTo("Result"));
        Assert.That(outputs[1].Name, Is.EqualTo("b"));
        Assert.That(outputs[2].Name, Is.EqualTo("c"));
    }

    #endregion

    #region Resource Type Tests

    [Test]
    public void TypeMapper_MapsTextureTypes()
    {
        Assert.That(SdslTypeMapper.GetClrType("Texture2D"), Is.EqualTo(typeof(Stride.Graphics.Texture)));
        Assert.That(SdslTypeMapper.GetClrType("Texture3D"), Is.EqualTo(typeof(Stride.Graphics.Texture)));
        Assert.That(SdslTypeMapper.GetClrType("TextureCube"), Is.EqualTo(typeof(Stride.Graphics.Texture)));
        Assert.That(SdslTypeMapper.GetClrType("RWTexture2D"), Is.EqualTo(typeof(Stride.Graphics.Texture)));
    }

    [Test]
    public void TypeMapper_MapsSamplerType()
    {
        Assert.That(SdslTypeMapper.GetClrType("SamplerState"), Is.EqualTo(typeof(Stride.Graphics.SamplerState)));
        Assert.That(SdslTypeMapper.GetClrType("sampler"), Is.EqualTo(typeof(Stride.Graphics.SamplerState)));
    }

    [Test]
    public void TypeMapper_MapsBufferTypes()
    {
        Assert.That(SdslTypeMapper.GetClrType("StructuredBuffer<float4>"), Is.EqualTo(typeof(Stride.Graphics.Buffer)));
        Assert.That(SdslTypeMapper.GetClrType("RWStructuredBuffer<float>"), Is.EqualTo(typeof(Stride.Graphics.Buffer)));
        Assert.That(SdslTypeMapper.GetClrType("Buffer<uint>"), Is.EqualTo(typeof(Stride.Graphics.Buffer)));
    }

    [Test]
    public void TypeMapper_IsTextureType()
    {
        Assert.That(SdslTypeMapper.IsTextureType("Texture2D"), Is.True);
        Assert.That(SdslTypeMapper.IsTextureType("RWTexture3D"), Is.True);
        Assert.That(SdslTypeMapper.IsTextureType("float"), Is.False);
        Assert.That(SdslTypeMapper.IsTextureType("Buffer<float>"), Is.False);
    }

    [Test]
    public void TypeMapper_IsBufferType()
    {
        Assert.That(SdslTypeMapper.IsBufferType("StructuredBuffer<float>"), Is.True);
        Assert.That(SdslTypeMapper.IsBufferType("RWBuffer<uint>"), Is.True);
        Assert.That(SdslTypeMapper.IsBufferType("Texture2D"), Is.False);
    }

    [Test]
    public void TypeMapper_IsSamplerType()
    {
        Assert.That(SdslTypeMapper.IsSamplerType("SamplerState"), Is.True);
        Assert.That(SdslTypeMapper.IsSamplerType("sampler"), Is.True);
        Assert.That(SdslTypeMapper.IsSamplerType("Texture2D"), Is.False);
    }

    [Test]
    public void FunctionParser_ParsesTextureParameter()
    {
        var parser = new MixinFunctionParser();
        var content = @"shader TestMixin
{
    // @export
    float4 sampleTex(Texture2D tex, SamplerState samp, float2 uv)
    {
        return tex.Sample(samp, uv);
    }
}";

        var functions = parser.ParseContent(content, "test.sdsl");

        Assert.That(functions.Count, Is.EqualTo(1));
        Assert.That(functions[0].Parameters.Count, Is.EqualTo(3));
        Assert.That(functions[0].Parameters[0].SdslType, Is.EqualTo("Texture2D"));
        Assert.That(functions[0].Parameters[0].ClrType, Is.EqualTo(typeof(Stride.Graphics.Texture)));
        Assert.That(functions[0].Parameters[1].SdslType, Is.EqualTo("SamplerState"));
        Assert.That(functions[0].Parameters[1].ClrType, Is.EqualTo(typeof(Stride.Graphics.SamplerState)));
    }

    #endregion
}

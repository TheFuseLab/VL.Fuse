using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Fuse;
using Fuse.compute;
using Fuse.ShaderFX;
using NUnit.Framework;
using Stride.Core.IO;
using Stride.Core.Shaders.Utility;
using Stride.Graphics;
using Stride.Rendering.Materials;
using Stride.Shaders;
using Stride.Shaders.Compiler;
using Stride.Shaders.Parser;
using Stride.Shaders.Parser.Mixins;
using VL.Stride.Shaders.ShaderFX;

namespace PatchTests;

internal static class ShaderCompilerTestUtil
{
    public static (EffectBytecodeCompilerResult Result, string[] Errors)
        CompileGeneratedShaderWithStandaloneEffectCompiler(ToComputeFx<GpuVoid> computeFx)
    {
        var diagnostics = computeFx.LastDiagnosticContext;
        Assert.That(diagnostics, Is.Not.Null);
        Assert.That(computeFx.ShaderCode, Is.Not.Null.And.Not.Empty);

        var fileProvider = new FileSystemProvider(
            $"/fuse-standalone-compile-test-{Guid.NewGuid():N}",
            Environment.CurrentDirectory);
        var compiler = new EffectCompiler(fileProvider);
        var sourceManager = GetShaderSourceManager(compiler);
        RegisterShaderSourceFromFile(sourceManager, "ComputeVoid", FindComputeVoidSource());
        RegisterShaderSourceFromFile(sourceManager, "ComputeShaderBase", FindComputeShaderBaseSource());
        sourceManager.AddShaderSource(diagnostics.ShaderName, computeFx.ShaderCode, diagnostics.SourcePath);

        var mixin = new ShaderMixinSource { Name = diagnostics.ShaderName };
        mixin.Mixins.Add(new ShaderClassSource(diagnostics.ShaderName));

        var effectParameters = new EffectCompilerParameters
        {
            Platform = GraphicsPlatform.Direct3D11,
            Profile = GraphicsProfile.Level_11_0,
            Debug = true,
            OptimizationLevel = 0
        };
        var compilerParameters = new CompilerParameters { EffectParameters = effectParameters };

        var result = compiler.Compile(mixin, effectParameters, compilerParameters).WaitForResult();
        var messages = result.CompilationLog?.Messages?.ToArray() ?? [];
        var errors = messages
            .Where(message => message.Type >= Stride.Core.Diagnostics.LogMessageType.Error)
            .Select(message => message.ToString())
            .ToArray();

        return (result, errors);
    }

    private static ShaderSourceManager GetShaderSourceManager(EffectCompiler compiler)
    {
        var method = typeof(EffectCompiler).GetMethod(
            "GetMixinParser",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null);
        return ((ShaderMixinParser)method.Invoke(compiler, null)).SourceManager;
    }

    private static void RegisterShaderSourceFromFile(
        ShaderSourceManager sourceManager,
        string shaderName,
        string sourcePath)
    {
        Assert.That(sourcePath, Is.Not.Null, $"Could not locate {shaderName}.sdsl for standalone compile test.");
        sourceManager.AddShaderSource(shaderName, File.ReadAllText(sourcePath), sourcePath);
    }

    private static string FindComputeVoidSource()
    {
        var configuredPath = Environment.GetEnvironmentVariable("FUSE_TEST_COMPUTE_VOID");
        if (File.Exists(configuredPath))
            return configuredPath;

        var relativePath = Path.Combine(
            "stride",
            "Assets",
            "Effects",
            "ShaderFX",
            "ComputeVoid",
            "ComputeVoid.sdsl");

        return FindFirstExistingPath(
            Path.Combine(TestContext.CurrentContext.TestDirectory, relativePath),
            Path.Combine(Environment.CurrentDirectory, relativePath),
            Path.Combine(AppContext.BaseDirectory, relativePath))
            ?? FindFirstFileNamed(
                "ComputeVoid.sdsl",
                Environment.CurrentDirectory,
                TestContext.CurrentContext.TestDirectory,
                AppContext.BaseDirectory,
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..")),
                FindSiblingDirectoryUpwards("VL.StandardLibs-main", Environment.CurrentDirectory),
                FindSiblingDirectoryUpwards("VL.StandardLibs-main", AppContext.BaseDirectory));
    }

    private static string FindComputeShaderBaseSource()
    {
        var configuredPath = Environment.GetEnvironmentVariable("FUSE_TEST_COMPUTE_SHADER_BASE");
        if (File.Exists(configuredPath))
            return configuredPath;

        return FindFirstExistingPath(
            Path.Combine(Environment.CurrentDirectory, "vl", "shaders", "ComputeShaderBase.sdsl"),
            Path.Combine(TestContext.CurrentContext.TestDirectory, "vl", "shaders", "ComputeShaderBase.sdsl"),
            Path.Combine(AppContext.BaseDirectory, "vl", "shaders", "ComputeShaderBase.sdsl"))
            ?? FindRelativeFileUpwards(
                Path.Combine("vl", "shaders", "ComputeShaderBase.sdsl"),
                Environment.CurrentDirectory,
                TestContext.CurrentContext.TestDirectory,
                AppContext.BaseDirectory);
    }

    private static string FindFirstExistingPath(params string[] paths)
    {
        return paths.FirstOrDefault(File.Exists);
    }

    private static string FindFirstFileNamed(string fileName, params string[] roots)
    {
        foreach (var root in roots.Where(root => !string.IsNullOrEmpty(root) && Directory.Exists(root)))
        {
            var path = Directory
                .EnumerateFiles(root, fileName, SearchOption.AllDirectories)
                .FirstOrDefault(File.Exists);

            if (path != null)
                return path;
        }

        return null;
    }

    private static string FindSiblingDirectoryUpwards(string directoryName, string startPath)
    {
        var directory = Directory.Exists(startPath)
            ? new DirectoryInfo(startPath)
            : Directory.GetParent(startPath);

        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, directoryName);
            if (Directory.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        return null;
    }

    private static string FindRelativeFileUpwards(string relativePath, params string[] startPaths)
    {
        foreach (var startPath in startPaths.Where(path => !string.IsNullOrEmpty(path)))
        {
            var directory = Directory.Exists(startPath)
                ? new DirectoryInfo(startPath)
                : Directory.GetParent(startPath);

            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, relativePath);
                if (File.Exists(candidate))
                    return candidate;

                directory = directory.Parent;
            }
        }

        return null;
    }
}

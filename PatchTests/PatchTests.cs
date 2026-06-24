using System;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Fuse;
using Fuse.compute;
using Fuse.ShaderFX;
using Stride.Rendering.Materials;
using VL.TestFramework;
using VL.Core;

namespace Fuse.Tests
{
    [TestFixture]
    public class PatchTests
    {
        TestEnvironment testEnvironment;
        private static readonly object StrideShaderIncludeLock = new();
        private static readonly HashSet<string> TemporaryShaderIncludeFiles = new(StringComparer.OrdinalIgnoreCase);

        // Watch out: Don't use async Task here. NUnit keeps a synchronization context per async method,
        // subsequent calls to the session therefor fail with the exception that the context had been shut down.
        // See https://github.com/nunit/nunit/issues/3500
        [OneTimeSetUp]
        public void Setup()
        {
            testEnvironment = CreateEnvironment();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            testEnvironment?.Dispose();
            testEnvironment = null;
            CleanupTemporaryShaderIncludes();
        }

        private static TestEnvironment CreateEnvironment()
        {
            EnsureStrideShaderIncludesVisible();

            var vvvvExePath = Environment.GetEnvironmentVariable("FUSE_TEST_VVVV_EXE")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "vvvv", "vvvv_gamma_7.0-win-x64", "vvvv.exe");
            return TestEnvironmentLoader.Load(vvvvExePath, [MainLibPath, RepositoriesPath]);
        }

        private static void EnsureStrideShaderIncludesVisible()
        {
            var source = FindComputeShaderBaseSource();
            var target = Path.Combine(MainLibPath, "VL.Fuse", "vl", "shaders", "ComputeShaderBase.sdsl");

            if (source == null)
                return;

            lock (StrideShaderIncludeLock)
            {
                if (File.Exists(target))
                    return;

                var targetDirectory = Path.GetDirectoryName(target);
                if (targetDirectory != null)
                    Directory.CreateDirectory(targetDirectory);
                File.Copy(source, target);
                TemporaryShaderIncludeFiles.Add(target);
                AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            }
        }

        private static string FindComputeShaderBaseSource()
        {
            var configuredPath = Environment.GetEnvironmentVariable("FUSE_TEST_COMPUTE_SHADER_BASE");
            if (File.Exists(configuredPath))
                return configuredPath;

            var strideRenderingPath = Path.Combine(NuGetPackagesPath, "stride.rendering");
            if (!Directory.Exists(strideRenderingPath))
                return null;

            return Directory.GetDirectories(strideRenderingPath)
                .OrderByDescending(versionDirectory => Path.GetFileName(versionDirectory))
                .Select(versionDirectory => Path.Combine(
                    versionDirectory,
                    "stride",
                    "Assets",
                    "ComputeEffect",
                    "ComputeShaderBase.sdsl"))
                .Where(File.Exists)
                .FirstOrDefault();
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            CleanupTemporaryShaderIncludes();
        }

        private static void CleanupTemporaryShaderIncludes()
        {
            lock (StrideShaderIncludeLock)
            {
                foreach (var filePath in TemporaryShaderIncludeFiles.ToArray())
                {
                    try
                    {
                        if (File.Exists(filePath))
                            File.Delete(filePath);
                    }
                    catch
                    {
                        // Best effort cleanup only; the file is regenerated on the next test run.
                    }

                    TemporaryShaderIncludeFiles.Remove(filePath);
                }
            }
        }

        public static IEnumerable<TestCaseData> NormalPatches()
        {
            using var testEnv = CreateEnvironment();
            foreach (var p in testEnv.GetPackages())
            {
                if (p.Identity.Id != "VL.Fuse")
                    continue;

                foreach (var f in p.Files)
                {
                    if (Path.GetExtension(f.AbsolutePath) != ".vl")
                        continue;

                    if (f.PackagePath.Contains("R&D"))
                        continue;
                    if (f.PackagePath.Contains("help obsolete"))
                        continue;
                    if (f.PackagePath.Contains("help_to_check"))
                        continue;

                    var testCase = new TestCaseData(f.AbsolutePath)
                        .SetCategory(p.Identity.ToString())
                        .SetArgDisplayNames(f.PackagePath);

                    if (f.PackagePath.Replace('\\', '/') == "vl/Fuse.Compute.vl")
                        testCase = testCase.SetCategory("FuseComputeCore");

                    if (f.PackagePath.Contains("Nodevember23"))
                        testCase = testCase.Ignore("Needs Fuse.SketchBook");

                    yield return testCase;
                }
            }
        }


        public static string MainLibPath;
        public static string RepositoriesPath;
        public static string NuGetPackagesPath;

        static PatchTests()
        {
            MainLibPath = Environment.GetEnvironmentVariable("FUSE_TEST_MAIN_LIB_PATH") ?? @"D:\development\vl\repo";
            RepositoriesPath = Environment.GetEnvironmentVariable("FUSE_TEST_REPOSITORIES_PATH")
                ?? @"C:\Users\Christian\AppData\Local\vvvv\gamma\nugets";
            NuGetPackagesPath = Environment.GetEnvironmentVariable("FUSE_TEST_NUGET_PACKAGES_PATH")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        }


        /// <summary>
        /// Checks if the document comes with compile time errors (e.g. red nodes). Doesn't actually run the patches.
        /// </summary>
        /// <param name="filePath"></param>
        [TestCaseSource(nameof(NormalPatches))]
        public async Task IsntRed(string filePath)
        {
            try
            {
                await testEnvironment.LoadAndTestAsync(filePath);
            }
            catch (AssertionException ex)
            {
                var failure = BuildFailureAnalysis(filePath, ex);
                if (failure.HasOnlyKnownExternalPackageCascadeErrors)
                    Assert.Ignore(failure.Report);

                throw new AssertionException(failure.Report, ex);
            }
        }

        [Test]
        [Category("FuseShaderTrace")]
        public void GenerateMinimalComputeShaderTrace()
        {
            var previousTrace = ShaderNodesUtil.TraceShaderSource;
            var previousDirectory = ShaderNodesUtil.ShaderDumpDirectory;
            var previousTiming = ShaderNodesUtil.TimeShaderGeneration;
            var dumpDirectory = Path.Combine(TestContext.CurrentContext.WorkDirectory, "shader-trace");

            try
            {
                if (Directory.Exists(dumpDirectory))
                    Directory.Delete(dumpDirectory, recursive: true);
                Directory.CreateDirectory(dumpDirectory);

                ShaderNodesUtil.TraceShaderSource = true;
                ShaderNodesUtil.ShaderDumpDirectory = dumpDirectory;
                ShaderNodesUtil.TimeShaderGeneration = true;

                var host = GetProperty(testEnvironment, "Host");
                var appHost = host as AppHost;
                if (appHost == null)
                    Assert.Fail($"TestEnvironment host is not a VL AppHost: {host?.GetType().FullName ?? "<null>"}");

                using var appHostScope = appHost.MakeCurrent();
                var rootContext = NodeContext.Create(appHost).CreateSubContext("FuseShaderTrace", "Root");
                var target = new ValueInput<float>(
                    rootContext.CreateSubContext("FuseShaderTrace", "Target"),
                    "traceTarget");
                var source = new ValueInput<float>(
                    rootContext.CreateSubContext("FuseShaderTrace", "Source"),
                    "traceSource");
                var assign = new AssignValue<float>(
                    rootContext.CreateSubContext("FuseShaderTrace", "Assign"),
                    target,
                    source);

                var computeFx = new ToComputeFx<GpuVoid>(assign);
                computeFx.GenerateShaderSource(new ShaderGeneratorContext(), null);

                var addShaderSourceFailures = Directory.GetFiles(dumpDirectory, "*_addshadersource_exception.log");
                foreach (var failureLog in addShaderSourceFailures)
                {
                    // VL.TestFramework provides an AppHost but does not start a Stride Game service.
                    TestContext.AddTestAttachment(failureLog, "Fuse AddShaderSource diagnostics");
                    var failureText = File.ReadAllText(failureLog);
                    Assert.That(
                        failureText,
                        Does.Not.Contain("No app host is installed on the current thread"),
                        $"AddShaderSource ran without a current AppHost. Failure log: {failureLog}");
                    Assert.That(
                        failureText,
                        Does.Contain("IResourceProvider`1[Stride.Engine.Game]"),
                        $"Unexpected AddShaderSource failure. Failure log: {failureLog}");
                }

                var diagnosticFiles = Directory.GetFiles(dumpDirectory, "*_diagnostics.log");
                Assert.That(
                    diagnosticFiles,
                    Is.Not.Empty,
                    $"No shader diagnostics were written to {dumpDirectory}.");

                var summaryPath = Path.Combine(dumpDirectory, "shader-trace-summary.txt");
                File.WriteAllText(summaryPath, BuildShaderTraceSummary(diagnosticFiles));
                TestContext.AddTestAttachment(summaryPath, "Fuse shader trace timing summary");
                foreach (var diagnosticFile in diagnosticFiles.Take(5))
                    TestContext.AddTestAttachment(diagnosticFile, "Fuse shader diagnostics");

                TestContext.WriteLine(File.ReadAllText(summaryPath));
            }
            finally
            {
                ShaderNodesUtil.TraceShaderSource = previousTrace;
                ShaderNodesUtil.ShaderDumpDirectory = previousDirectory;
                ShaderNodesUtil.TimeShaderGeneration = previousTiming;
            }
        }

        private static string BuildShaderTraceSummary(IEnumerable<string> diagnosticFiles)
        {
            var builder = new StringBuilder();
            var files = diagnosticFiles.OrderBy(path => path).ToList();
            builder.AppendLine($"Shader diagnostic files: {files.Count}");

            foreach (var file in files)
            {
                var lines = File.ReadAllLines(file);
                var shader = lines.FirstOrDefault(line => line.StartsWith("Shader:", StringComparison.Ordinal))
                    ?? $"Shader: {Path.GetFileName(file)}";
                var phase = lines.FirstOrDefault(line => line.StartsWith("Phase:", StringComparison.Ordinal))
                    ?? "Phase: <unknown>";
                var timings = ParseTimingLines(lines).OrderByDescending(t => t.ElapsedMilliseconds).ToList();

                builder.AppendLine();
                builder.AppendLine(shader);
                builder.AppendLine(phase);
                builder.AppendLine("Top timings:");
                foreach (var timing in timings.Take(10))
                    builder.AppendLine($"  {timing.Name}: {timing.ElapsedMilliseconds:0.###} ms");

                if (timings.Count == 0)
                    builder.AppendLine("  <none>");
            }

            return builder.ToString();
        }

        private static IEnumerable<(string Name, double ElapsedMilliseconds)> ParseTimingLines(IEnumerable<string> lines)
        {
            var timingPattern = new Regex(@"^\s*-\s*(?<name>.+):\s*(?<elapsed>\d+(?:\.\d+)?)\s*ms\s*$");
            var inTimingSection = false;

            foreach (var line in lines)
            {
                if (line == "Timings:")
                {
                    inTimingSection = true;
                    continue;
                }

                if (!inTimingSection)
                    continue;

                if (!line.StartsWith("  ", StringComparison.Ordinal))
                    yield break;

                var match = timingPattern.Match(line);
                if (!match.Success)
                    continue;

                if (double.TryParse(
                        match.Groups["elapsed"].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var elapsedMilliseconds))
                {
                    yield return (match.Groups["name"].Value, elapsedMilliseconds);
                }
            }
        }

        private FailureAnalysis BuildFailureAnalysis(string filePath, AssertionException originalException)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"VL patch failed: {filePath}");
            builder.AppendLine($"MainLibPath: {MainLibPath}");
            builder.AppendLine($"RepositoriesPath: {RepositoriesPath}");
            builder.AppendLine($"NuGetPackagesPath: {NuGetPackagesPath}");
            builder.AppendLine($"Original assertion: {originalException.Message}");
            var hasOnlyKnownExternalPackageCascadeErrors = false;

            try
            {
                var host = GetProperty(testEnvironment, "Host");
                if (host == null)
                {
                    builder.AppendLine("No TestEnvironment host available.");
                    return new FailureAnalysis(builder.ToString(), hasOnlyKnownExternalPackageCascadeErrors);
                }

                var compilation = GetProperty(host, "LatestCompilation");
                var messages = GetCompilationMessages(compilation);
                var localMessages = messages
                    .Where(message => MessageBelongsToFile(host, message, filePath))
                    .ToList();
                var externalMessages = messages.Except(localMessages).ToList();

                builder.AppendLine($"Compiler errors: {messages.Count}");
                builder.AppendLine($"Compiler errors in tested document: {localMessages.Count}");

                if (messages.Count == 0)
                {
                    builder.AppendLine("No Error/Critical messages found on LatestCompilation.");
                    return new FailureAnalysis(builder.ToString(), hasOnlyKnownExternalPackageCascadeErrors);
                }

                hasOnlyKnownExternalPackageCascadeErrors = localMessages.Count > 0
                    && localMessages.All(IsKnownExternalPackageCascadeMessage);
                if (hasOnlyKnownExternalPackageCascadeErrors)
                {
                    builder.AppendLine(
                        "Only known external Stride package cascade errors were found in the tested document.");
                }

                AppendMessages(builder, "Errors in tested document", localMessages, host, 40);
                AppendMessages(builder, "Other compiler errors", externalMessages, host, 20);
            }
            catch (Exception ex)
            {
                builder.AppendLine($"Failed to collect VL diagnostics: {ex}");
            }

            return new FailureAnalysis(builder.ToString(), hasOnlyKnownExternalPackageCascadeErrors);
        }

        private sealed record FailureAnalysis(string Report, bool HasOnlyKnownExternalPackageCascadeErrors);

        private static void AppendMessages(
            StringBuilder builder,
            string title,
            List<object> messages,
            object host,
            int limit)
        {
            builder.AppendLine();
            builder.AppendLine($"{title}: {messages.Count}");

            foreach (var message in messages.Take(limit))
                AppendMessage(builder, message, host);

            if (messages.Count > limit)
                builder.AppendLine($"... {messages.Count - limit} more {title.ToLowerInvariant()} omitted.");
        }

        private static List<object> GetCompilationMessages(object compilation)
        {
            if (compilation == null)
                return new List<object>();

            var getMessages = compilation.GetType().GetMethod(
                "GetMessages",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(bool) },
                null);

            if (getMessages?.Invoke(compilation, new object[] { true }) is not System.Collections.IEnumerable allMessages)
                return new List<object>();

            return allMessages.Cast<object>()
                .Where(IsErrorMessage)
                .OrderBy(m => GetProperty(m, "Location")?.ToString())
                .ThenBy(m => GetField(m, "What")?.ToString())
                .ToList();
        }

        private static bool MessageBelongsToFile(object host, object message, string filePath)
        {
            var location = GetField(message, "Location");
            var documentPath = TryGetDocumentPath(host, location)?.ToString();
            return string.Equals(documentPath, filePath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsErrorMessage(object message)
        {
            var severity = GetField(message, "Severity");
            var severityName = severity?.ToString();
            return severityName == "Error" || severityName == "Critical";
        }

        private static bool IsKnownExternalPackageCascadeMessage(object message)
        {
            return IsKnownExternalPackageCascadeMessageText(GetField(message, "What")?.ToString());
        }

        internal static bool IsKnownExternalPackageCascadeMessageText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            if (!text.EndsWith("has errors.", StringComparison.OrdinalIgnoreCase))
                return false;

            var categoryStart = text.IndexOf('[');
            var categoryEnd = text.IndexOf(']', categoryStart + 1);
            if (categoryStart < 0 || categoryEnd <= categoryStart)
                return false;

            var category = text.Substring(categoryStart + 1, categoryEnd - categoryStart - 1);
            return category.StartsWith("Stride.", StringComparison.OrdinalIgnoreCase)
                || string.Equals(category, "Nvidia.CUDA", StringComparison.OrdinalIgnoreCase);
        }

        private static void AppendMessage(StringBuilder builder, object message, object host)
        {
            var location = GetField(message, "Location");
            builder.AppendLine();
            builder.AppendLine($"[{GetField(message, "Severity")}] {GetField(message, "What")}");
            AppendIfPresent(builder, "Why", GetField(message, "Why"));
            AppendIfPresent(builder, "How", GetField(message, "How"));
            AppendIfPresent(builder, "Source", GetField(message, "Source"));
            AppendIfPresent(builder, "Symbol", GetField(message, "Symbol"));
            AppendIfPresent(builder, "Location", location);

            var element = TryGetElement(host, location);
            if (element == null)
                return;

            AppendIfPresent(builder, "Element", element);
            AppendIfPresent(builder, "Element kind", GetProperty(element, "Kind"));
            AppendIfPresent(builder, "Element name", GetProperty(element, "Name"));
            AppendIfPresent(builder, "Element path", TryGetDocumentPath(host, location));
        }

        private static object TryGetElement(object host, object location)
        {
            if (host == null || location == null)
                return null;

            var solution = GetProperty(host, "CurrentSolution");
            var getDescendent = solution?.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "GetDescendent"
                    && m.GetParameters().Length == 1
                    && m.GetParameters()[0].ParameterType == location.GetType());

            try
            {
                return getDescendent?.Invoke(solution, new[] { location });
            }
            catch
            {
                return null;
            }
        }

        private static object TryGetDocumentPath(object host, object location)
        {
            if (host == null || location == null)
                return null;

            var getDocumentPath = host.GetType().GetMethod(
                "GetDocumentPath",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { location.GetType() },
                null);

            try
            {
                return getDocumentPath?.Invoke(host, new[] { location });
            }
            catch
            {
                return null;
            }
        }

        private static void AppendIfPresent(StringBuilder builder, string label, object value)
        {
            if (value == null)
                return;

            var text = value.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return;

            builder.AppendLine($"{label}: {text}");
        }

        private static object GetProperty(object instance, string name)
        {
            return instance?.GetType()
                .GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(instance);
        }

        private static object GetField(object instance, string name)
        {
            return instance?.GetType()
                .GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(instance);
        }
    }
}

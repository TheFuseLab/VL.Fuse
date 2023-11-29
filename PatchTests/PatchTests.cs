using System;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using VL.TestFramework;

namespace Fuse.Tests
{
    [TestFixture]
    public class PatchTests
    {
        TestEnvironment testEnvironment;

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
            testEnvironment.Dispose();
            testEnvironment = null;
        }

        private static TestEnvironment CreateEnvironment()
        {
            var vvvvExePath = Task.Run(() => TestEnvironmentLoader.DownloadEntryAssemblyAsync());
            Console.WriteLine("YP" + vvvvExePath);
            return TestEnvironmentLoader.Load(vvvvExePath.Result, new[] { MainLibPath, RepositoriesPath });
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

                    var testCase = new TestCaseData(f.AbsolutePath)
                        .SetCategory(p.Identity.ToString())
                        .SetArgDisplayNames(f.PackagePath);

                    yield return testCase;
                }
            }
        }


        public static string MainLibPath;
        public static string RepositoriesPath;

        static PatchTests()
        {
            var currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            MainLibPath = Path.GetFullPath(Path.Combine(currentDirectory, @"..\..\..\..\"));
            RepositoriesPath = Path.GetFullPath(Path.Combine(MainLibPath, @".."));
        }


        /// <summary>
        /// Checks if the document comes with compile time errors (e.g. red nodes). Doesn't actually run the patches.
        /// </summary>
        /// <param name="filePath"></param>
        [TestCaseSource(nameof(NormalPatches))]
        public async Task IsntRed(string filePath)
        {
            await testEnvironment.LoadAndTestAsync(filePath);
        }
    }
}

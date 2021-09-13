using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using VL.Lang;
using VL.Lang.Symbols;
using VL.Model;
using VVVV.NuGetAssemblyLoader;

namespace MyTests
{
    enum SaveDocCondition { Never, WhenGreen, Always };

    [TestFixture]
    public class PatchTests
    {
        static string[] Packs = new string[]{ 
        
        //  FIX ME !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            @"C:\Program Files\vvvv\vvvv_gamma_2021.4.0-0387-g2b032b9108\lib\packs",

        };

        // DO YOU WANT TO SAVE THE VL DOCS TO DISK? 
        static SaveDocCondition SaveDocCondition = SaveDocCondition.Never;


        public static IEnumerable<string> NormalPatches()
        {
            var allVLDocs = Directory.GetFiles(MainLibPath, "*.vl", SearchOption.AllDirectories);
            var allVLDocsWithoutRnD = allVLDocs.Where(f => !f.Contains("R&D"));

            var pathUri = new Uri(MainLibPath, UriKind.Absolute);
            // Yield all your VL docs1
            //foreach (var file in allVLDocs)
            foreach (var file in allVLDocsWithoutRnD)
            {
                var fileUri = new Uri(file, UriKind.Absolute);
                yield return Uri.UnescapeDataString(pathUri.MakeRelativeUri(fileUri).ToString()).Replace("/", @"\");
            }
        }



        public static readonly VLSession Session;
        public static string MainLibPath;
        public static string RepositoriesPath;

        static PatchTests()
        {
            var currentDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            MainLibPath = Path.GetFullPath(Path.Combine(currentDirectory, @"..\..\..\..\..\"));
            RepositoriesPath = Path.GetFullPath(Path.Combine(MainLibPath, @".."));

            foreach (var pack in Packs)
                AssemblyLoader.AddPackageRepositories(pack);

            // Also add the "vl-libs" folder. The folder that contains our library.
            AssemblyLoader.AddPackageRepositories(RepositoriesPath);


            if (SynchronizationContext.Current == null)
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());


            Session = new VLSession("gamma", SynchronizationContext.Current, includeUserPackages: false)
            {
                CheckSolution = false,
                IgnoreDynamicEnumErrors = true,
                NoCache = true,
                KeepTargetCode = false
            };
        }

        static Solution FCompiledSolution;


        /// <summary>
        /// Checks if the document comes with compile time errors (e.g. red nodes). Doesn't actually run the patches.
        /// </summary>
        /// <param name="filePath"></param>
        [TestCaseSource(nameof(NormalPatches))]
        public static async Task IsntRed(string filePath)
        {
            filePath = Path.Combine(MainLibPath, filePath);
            var solution = FCompiledSolution ?? (FCompiledSolution = await CompileAsync(NormalPatches()));
            var document = solution.GetOrAddDocument(filePath);

            // Check document structure
            Assert.True(document.IsValid);

            // Check dependenices
            foreach (var dep in document.GetDocSymbols().Dependencies)
                Assert.IsFalse(dep.RemoteSymbolSource is Dummy, $"Couldn't find dependency {dep}");

            // Check all containers and process node definitions, including application entry point
            CheckNodes(document.AllTopLevelDefinitions);

            if (SaveDocCondition == SaveDocCondition.Always || (SaveDocCondition == SaveDocCondition.WhenGreen && Success()))
                document.Save(isTrusted: false); // TODO: discuss when this can be turned on.
        }

        private static bool Success()
        {
            var thisTest = TestExecutionContext.CurrentContext.CurrentTest;
            var testResult = thisTest.MakeTestResult();
            var resultState = testResult.ResultState;
            return resultState == ResultState.Success || resultState == ResultState.Inconclusive;
        }

        static async Task<Solution> CompileAsync(IEnumerable<string> docs)
        {
            var solution = Session.CurrentSolution;
            foreach (var f in docs)
                solution = solution.GetOrAddDocument(Path.Combine(MainLibPath, f)).Solution;
            return await solution.WithFreshCompilationAsync();
        }

        public static void CheckNodes(IEnumerable<Node> nodes)
        {
            Parallel.ForEach(nodes, definition =>
            {
                var definitionSymbol = definition.GetSymbol() as IDefinitionSymbol;
                Assert.IsNotNull(definitionSymbol, $"No symbol for {definition}.");
                var errorMessages = definitionSymbol.Messages.Where(m => m.Severity == MessageSeverity.Error);
                Assert.That(errorMessages.None(), () => $"{definition}: {string.Join(Environment.NewLine, errorMessages)}");
                Assert.IsFalse(definitionSymbol.IsUnused, $"The symbol of {definition} is marked as unused.");
            });
        }




        // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        // Running Tests patches not supported yet. We for now can only check for compile time errors (like red nodes)


        /// <summary>
        /// Yield all test patches that shall run
        /// </summary>
        public static IEnumerable<string> TestPatches()
        {
            yield return $@"C:\dev\vl-libs\VL.DemoLib\src\NUnitTests\tests\tests.vl";
        }
    }
}

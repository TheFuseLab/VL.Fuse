using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using JetBrains.Profiler.Api;
using Stride.Core.Yaml.Tokens;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;


namespace Fuse.ShaderFX
{

    public abstract class AbstractStage 
    {
        public readonly string Key;

        public AbstractShaderNode StageNode;

        public int Ticket { get; private set; }

        public AbstractStage(string theKey, AbstractShaderNode theShaderNode)
        {
            
            
            Key = theKey;
            StageNode = theShaderNode;
            Ticket = 0;
        }
        
        public virtual void AppendInputs(Dictionary<string, string> theTemplateMap)
        {
        }

        public abstract string Source();
        public void ChangeGraph(AbstractShaderNode theNode)
        {
            Ticket++;
        }
    }

    public abstract class AbstractToShaderFX<T> : IComputeValue<T>
    {

        private readonly Dictionary<string, string> _customTemplate;
        
        public string ShaderCode { get; private set; }

        public string ShaderName { get; private set; }

        private readonly List<AbstractStage> _stages;

        private readonly HashSet<string>_declarations = new();
        private readonly HashSet<string>_groupDeclarations = new();
        private readonly HashSet<string>_structs = new();
        private readonly HashSet<string>_constantArrays = new();
        private readonly HashSet<string>_streams = new();
        private readonly HashSet<string>_mixins = new();
        private readonly HashSet<IComposition>_compositions = new();
        private readonly Dictionary<string, string> _functionMap = new();

        private readonly string _sourceTemplate;
        
        private readonly bool _isCompute;
        
        protected AbstractToShaderFX(
            List<AbstractStage> theStages, 
            Dictionary<string,string> theCustomTemplate, 
            bool theIsCompute,
            string theSource) 
        {
            _customTemplate = theCustomTemplate;
            _isCompute = theIsCompute;
            
            _stages = theStages;
            _sourceTemplate = theSource;

            _stages = theStages;
            Inputs = new Dictionary<string, AbstractShaderNode>();
            var stageCodeMap = new Dictionary<string, string>();
            foreach (var stage in _stages)
            {
                if (stage?.StageNode == null) continue;
                Inputs.Add(stage.Key, stage.StageNode);
                stageCodeMap.Add("stage" + stage.Key, stage.Source());
            }

            _sourceTemplate = ShaderNodesUtil.Evaluate(_sourceTemplate, stageCodeMap);
        }

        public Dictionary<string, AbstractShaderNode> Inputs { get; }

        public void AppendInputs(Dictionary<string, string> theTemplateMap)
        {
            foreach (var stage in _stages)
            {
                stage?.AppendInputs(theTemplateMap);
            }
        }

        public Dictionary<string, string> AppendTemplateValues(Dictionary<string, string> theTemplateMap) {
            
            theTemplateMap["shaderType"] = TypeHelpers.GetSignature<T>();
            theTemplateMap["resultType"] = TypeHelpers.GetGpuType<T>();
            
            AppendInputs(theTemplateMap);

            return theTemplateMap;
        }

        private Dictionary<string,string> CustomTemplates ()
        {
            return AppendTemplateValues(_customTemplate);
        }

       // private ParameterCollection _parameters;
        
        /// <summary>
        /// Gets the children.
        /// </summary>
        /// <param name="context">The context to get the children.</param>
        /// <returns>The list of children.</returns>
        public virtual IEnumerable<IComputeNode> GetChildren(object context = null)
        {
            return Enumerable.Empty<ComputeNode>();
        }

        private Dictionary<string, string> BuildTemplateMap()
        {
            var declarationBuilder = new StringBuilder();
            _declarations.ForEach(declaration => declarationBuilder.AppendLine(declaration));
            
            var groupDeclarationBuilder = new StringBuilder();
            _groupDeclarations.ForEach(declaration => groupDeclarationBuilder.AppendLine(declaration));
            
            var structBuilder = new StringBuilder();
            _structs.ForEach(gpuStruct => structBuilder.AppendLine(gpuStruct));
            
            var constantArrayBuilder = new StringBuilder();
            _constantArrays.ForEach(array => constantArrayBuilder.AppendLine(array));
            
            var streamBuilder = new StringBuilder();
            _streams.ForEach(stream => streamBuilder.AppendLine(stream));
            
            var mixinBuilder = new StringBuilder();
            _mixins.ForEach(mixin => mixinBuilder.Append(", " + mixin));

            var compositionBuilder = new StringBuilder();
            _compositions.ForEach(composition => compositionBuilder.AppendLine(composition.Declaration));
            
            var functionBuilder = new StringBuilder();
            _functionMap?.ForEach(kv => functionBuilder.AppendLine(kv.Value));


            return new Dictionary<string, string>
            {
                {"mixins", mixinBuilder.ToString()},
                {"declarations", declarationBuilder.ToString()},
                {"compositions", compositionBuilder.ToString()},
                {"structs", structBuilder.ToString()},
                {"constantArrays", constantArrayBuilder.ToString()},
                {"streams", streamBuilder.ToString()},
                {"functions", functionBuilder.ToString()},
                {"groupDeclarations", groupDeclarationBuilder.ToString()}
            };
        }

        private void HandleDeclaration(FieldDeclaration theDeclaration, bool theIsComputeShader)
        {
            if (theDeclaration.IsResource)
            {
                _groupDeclarations.Add(theDeclaration.GetDeclaration(theIsComputeShader));
            }
            else
            {
                _declarations.Add(theDeclaration.GetDeclaration(theIsComputeShader));
            }
        }

        private void HandleFunction(KeyValuePair<string,string> theKeyFunction)
        {
            if(!_functionMap.ContainsKey(theKeyFunction.Key))_functionMap.Add(theKeyFunction.Key, theKeyFunction.Value);
        }
        
        private void HandleShader(bool theIsComputeShader, AbstractShaderNode theShaderInput, string theKey, out string theSource, out string theStreams, out string theDefinedStreams)
        {
            var handleShaderWatch = new Stopwatch();
            handleShaderWatch.Start();
            if(ShaderNodesUtil.TimeShaderGeneration)Console.WriteLine($"  -> HandleShader: {theKey}");
            var streamBuilder = new StringBuilder();
            var streamDeclareBuilder = new StringBuilder();
            
            
            _stopwatch.Restart();
            theShaderInput.DeclarationList().ForEach(declaration => HandleDeclaration(declaration, theIsComputeShader));
            if(ShaderNodesUtil.TimeShaderGeneration)Console.WriteLine($"     Declaration: {_stopwatch.ElapsedMilliseconds} ms");
            
            _stopwatch.Restart();
            theShaderInput.StructList().ForEach(value => _structs.Add(value));
            if(ShaderNodesUtil.TimeShaderGeneration)Console.WriteLine($"     Structs: {_stopwatch.ElapsedMilliseconds} ms");
            
            _stopwatch.Restart();
            theShaderInput.ConstantArrayList().ForEach(value => _constantArrays.Add(value));
            if(ShaderNodesUtil.TimeShaderGeneration)Console.WriteLine($"     Arrays: {_stopwatch.ElapsedMilliseconds} ms");
            
            _stopwatch.Restart();
            theShaderInput.StreamList().ForEach(value => _streams.Add(value));
            if(ShaderNodesUtil.TimeShaderGeneration)Console.WriteLine($"     streams: {_stopwatch.ElapsedMilliseconds} ms");
            
            _stopwatch.Restart();
            theShaderInput.MixinList().ForEach(value => _mixins.Add(value));
            if(ShaderNodesUtil.TimeShaderGeneration)Console.WriteLine($"     Mixins: {_stopwatch.ElapsedMilliseconds} ms");

            _stopwatch.Restart();
            theShaderInput.CompositionList().ForEach(value => _compositions.Add(value));
            if(ShaderNodesUtil.TimeShaderGeneration)Console.WriteLine($"     Compositions: {_stopwatch.ElapsedMilliseconds} ms");

            _stopwatch.Restart();
            theShaderInput.FunctionMap().ForEach(HandleFunction);
            if(ShaderNodesUtil.TimeShaderGeneration)Console.WriteLine($"     Functions: {_stopwatch.ElapsedMilliseconds} ms");

            _stopwatch.Restart();
            streamBuilder.AppendLine("        streams. = " + theKey + theShaderInput.ID+";");
            streamDeclareBuilder.AppendLine("    stream " + TypeHelpers.GetGpuType(theShaderInput) + " " + theKey + ";");
            
            theSource = theShaderInput.BuildSourceCode();
            theStreams = streamBuilder.ToString();
            theDefinedStreams = streamDeclareBuilder.ToString();
            if(ShaderNodesUtil.TimeShaderGeneration)Console.WriteLine($"     Finish Stage: {handleShaderWatch.ElapsedMilliseconds} ms");
        }

        private string CheckCode(string theCode)
        {
            return _isCompute ? theCode : theCode.Replace("RWStructuredBuffer", "StructuredBuffer");
        }

        private readonly Stopwatch _stopwatch = new ();

        public ShaderSource GenerateShaderSource(ShaderGeneratorContext theContext, MaterialComputeColorKeys baseKeys)
        {
            //MeasureProfiler.StartCollectingData();
            _stopwatch.Reset();
            
            _stopwatch.Start();
            var watch = new Stopwatch();
            watch.Start();
            if(ShaderNodesUtil.TimeShaderGeneration)Console.WriteLine($"-> Start Generating Shader {ShaderName}");
            var sourceStream = new Dictionary<string,(string source, string stream)>();
            var streamDefinesBuilder = new StringBuilder();
            

            foreach (var kv in Inputs)
            {
                var shaderInput = kv.Value;
                shaderInput.CheckHashCodes();
                shaderInput.CheckContext(theContext);
                
                HandleShader(_isCompute, shaderInput, kv.Key, out var source, out var stream, out var streamDefines);
                sourceStream.Add(kv.Key,(source,stream));
                streamDefinesBuilder.AppendLine(streamDefines);
            }

            _stopwatch.Restart();
            var templateMap = BuildTemplateMap();
            templateMap["streamDeclaration"] = streamDefinesBuilder.ToString();
            CustomTemplates ().ForEach(kv =>
            {
                templateMap.Add(kv.Key,kv.Value);
            });
            
            sourceStream.ForEach(kv =>
            {
                templateMap.Add("source" + kv.Key,kv.Value.source);
                templateMap.Add("streams" + kv.Key,kv.Value.stream);
            });
            ShaderCode = ShaderNodesUtil.Evaluate(_sourceTemplate, templateMap);
            // ReSharper disable once VirtualMemberCallInConstructor
            ShaderCode = CheckCode(ShaderCode);
            ShaderCode = ShaderNodesUtil.FormatShaderCode(ShaderCode);
            ShaderName = "Shader_" + System.Math.Abs(ShaderCode.GetStableHashCode());
            //ShaderName = "Shader_" + ShaderNodesUtil.GetHashCode(_input.NodeContext);
            ShaderCode = ShaderNodesUtil.Evaluate(ShaderCode, new Dictionary<string, string>{{"shaderID",ShaderName}});
            
            ShaderCode = ShaderNodesUtil.Evaluate(ShaderCode, m => m.Groups["key"].Value.StartsWith("stage") ? "" : m.Value);

            foreach (var kv in Inputs)
            {
                kv.Value.ShaderCode = ShaderCode;
            }
            if(ShaderNodesUtil.TimeShaderGeneration)Console.WriteLine($"-> Evaluate: {_stopwatch.ElapsedMilliseconds} ms");

            var shaderSource = new ShaderMixinSource()
            {
                Name = ShaderName,
                Mixins = 
                {
                    new ShaderClassString(ShaderName, ShaderCode)
                }
            };

            foreach (var composition in _compositions)
            {
                var compositionSource = composition.ComputeNode.GenerateShaderSource(theContext, baseKeys);
                shaderSource.AddComposition(composition.CompositionName, compositionSource);
            }

            // _parameters = theContext.Parameters;

            // _input.InputList().ForEach(input => input.AddParameters(_parameters));
            //var result = new ShaderClassSource(ShaderName);
            _stopwatch.Stop();

           if(ShaderNodesUtil.TimeShaderGeneration)Console.WriteLine($"-> Execution Time: {watch.ElapsedMilliseconds} ms for Shader {ShaderName}");
           //MeasureProfiler.SaveData(ShaderName);
           //return result;

            return shaderSource;
        }
    }
}
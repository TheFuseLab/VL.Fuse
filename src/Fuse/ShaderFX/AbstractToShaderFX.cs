using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;


namespace Fuse.ShaderFX
{

    public abstract class AbstractStage : IChangeGraph
    {
        public readonly string Key;

        public readonly AbstractShaderNode StageNode;

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

        private readonly List<string> _definedStreams;

        private readonly List<AbstractStage> _stages;

        private readonly HashSet<string>_declarations = new();
        private readonly HashSet<string>_groupDeclarations = new();
        private readonly HashSet<string>_structs = new();
        private readonly HashSet<string>_constantArrays = new();
        private readonly HashSet<string>_streams = new();
        private readonly HashSet<string>_mixins = new();
        private readonly HashSet<string>_compositions = new();
        private readonly Dictionary<string, string> _functionMap = new();

        private readonly bool _isComputeShader;
        private readonly string _sourceTemplate;
        
        private readonly bool _isCompute;
        
        protected AbstractToShaderFX(
            List<AbstractStage> theStages, 
            List<string> theDefinedStreams, 
            Dictionary<string,string> theCustomTemplate, 
            bool theIsCompute,
            string theSource) 
        {
            _customTemplate = theCustomTemplate;
            _isCompute = theIsCompute;
            
            _stages = theStages;
            _definedStreams = theDefinedStreams;
            _isComputeShader = false;
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
            _compositions.ForEach(composition => compositionBuilder.AppendLine(composition));
            
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
            var streamBuilder = new StringBuilder();
            var streamDeclareBuilder = new StringBuilder();
            
            theShaderInput.DeclarationList().ForEach(declaration => HandleDeclaration(declaration, theIsComputeShader));
            theShaderInput.StructList().ForEach(value => _structs.Add(value));
            theShaderInput.ConstantArrayList().ForEach(value => _constantArrays.Add(value));
            theShaderInput.StreamList().ForEach(value => _streams.Add(value));
            theShaderInput.MixinList().ForEach(value => _mixins.Add(value));
            theShaderInput.CompositionList().ForEach(value => _compositions.Add(value));
            theShaderInput.FunctionMap().ForEach(HandleFunction);

            streamBuilder.AppendLine("        streams. = " + theKey + theShaderInput.ID+";");
            streamDeclareBuilder.AppendLine("    stream " + TypeHelpers.GetGpuType(theShaderInput) + " " + theKey + ";");
            
            theSource = theShaderInput.BuildSourceCode();
            theStreams = streamBuilder.ToString();
            theDefinedStreams = streamDeclareBuilder.ToString();
        }

        private string CheckCode(string theCode)
        {
            return _isCompute ? theCode : theCode.Replace("RWStructuredBuffer", "StructuredBuffer");
        }


        public ShaderSource GenerateShaderSource(ShaderGeneratorContext theContext, MaterialComputeColorKeys baseKeys)
        {
            
            var sourceStream = new Dictionary<string,(string source, string stream)>();
            var streamDefinesBuilder = new StringBuilder();
            

            foreach (var kv in Inputs)
            {
                kv.Value.CallCompileGraph();
                kv.Value.CheckContext(theContext);
                
                HandleShader(_isComputeShader, kv.Value, kv.Key, out var source, out var stream, out var streamDefines);
                sourceStream.Add(kv.Key,(source,stream));
                streamDefinesBuilder.AppendLine(streamDefines);
            }

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
            ShaderName = "Shader_" + Math.Abs(ShaderNodesUtil.GetStableHashCode(ShaderCode));
            //ShaderName = "Shader_" + ShaderNodesUtil.GetHashCode(_input.NodeContext);
            ShaderCode = ShaderNodesUtil.Evaluate(ShaderCode, new Dictionary<string, string>{{"shaderID",ShaderName}});
            
            ShaderCode = ShaderNodesUtil.Evaluate(ShaderCode, m => m.Groups["key"].Value.StartsWith("stage") ? "" : m.Value);

            foreach (var kv in Inputs)
            {
                kv.Value.ShaderCode = ShaderCode;
            }

            
            ShaderNodesUtil.AddShaderSource( ShaderName, ShaderCode, "shaders\\" + ShaderName + ".sdsl");
           // _parameters = theContext.Parameters;
            
           // _input.InputList().ForEach(input => input.AddParameters(_parameters));
            
            return new ShaderClassSource(ShaderName);
        }
    }
}
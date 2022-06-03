using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Engine;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

/*
 * var templateMap = new Dictionary<string, string>
            {
                {"resultName", theCompute.ID},
                {"resultType", TypeHelpers.GetGpuTypeForType<T>()},
                {"shaderType", TypeHelpers.GetSignatureTypeForType<T>()},
                {"sourceCompute", source},
                {"result", theCompute.ID}
            };
 */
namespace Fuse.ShaderFX
{
    public class DrawShaderNode : AbstractShaderNode
    {
        
        public DrawShaderNode(IDictionary<string,AbstractShaderNode> theVertexInputs) : base("drawShader")
        {
            Ins = new List<AbstractShaderNode>(theVertexInputs.Values);
            
        }

        public override string ID { get; }

        public override string TypeName()
        {
            return TypeHelpers.GetGpuTypeForType<float>();
        }

        protected override string SourceTemplate()
        {
            return "";
        }

        public override int Dimension()
        {
            return 0;
        }
    }
    
    public abstract class AbstractToShaderFX<T> : IComputeValue<T>
    {

        private readonly Dictionary<string, string> _customTemplate;
        private readonly IDictionary<string, AbstractShaderNode> _results;
        
        public string ShaderCode { get; private set; }

        public string ShaderName { get; private set; }

        private readonly List<string> _definedStreams;

        private readonly IDictionary<string, IDictionary<string, AbstractShaderNode>> _inputs;

        private readonly HashSet<string>_declarations = new HashSet<string>();
        private readonly HashSet<string>_groupDeclarations = new HashSet<string>();
        private readonly HashSet<string>_structs = new HashSet<string>();
        private readonly HashSet<string>_mixins = new HashSet<string>();
        private readonly HashSet<string>_compositions = new HashSet<string>();
        private readonly Dictionary<string, string> _functionMap = new Dictionary<string, string>();

        private readonly bool _isComputeShader;
        private readonly string _sourceTemplate;
        private readonly Game _game;
        
        private readonly bool _isCompute;
        
        protected AbstractToShaderFX(
            Game theGame,
            IDictionary<string,IDictionary<string,AbstractShaderNode>> theInputs, 
            IDictionary<string,AbstractShaderNode> theResults, 
            List<string> theDefinedStreams, 
            Dictionary<string,string> theCustomTemplate, 
            bool theIsCompute,
            string theSource) 
        {
            _customTemplate = theCustomTemplate;
            _isCompute = theIsCompute;
            _results = theResults;
            
            _inputs = theInputs;
            _definedStreams = theDefinedStreams;
            _isComputeShader = false;
            _sourceTemplate = theSource;
            _game = theGame;
        }

        private static Dictionary<string, string> AppendTemplateValues(
            Dictionary<string, string> theTemplateMap,
            IDictionary<string,AbstractShaderNode> theResults)
        {
            theTemplateMap["shaderType"] = TypeHelpers.GetSignatureTypeForType<T>();
            theTemplateMap["resultType"] = TypeHelpers.GetGpuTypeForType<T>();
            theResults.ForEach(kv => theTemplateMap["result" + kv.Key] = kv.Value.ID);
            return theTemplateMap;
        }
        
        protected Dictionary<string,string> CustomTemplates ()
        {
            return AppendTemplateValues(_customTemplate,_results);
        }

        private ParameterCollection _parameters;
        
        /// <summary>
        /// Gets the children.
        /// </summary>
        /// <param name="context">The context to get the children.</param>
        /// <returns>The list of children.</returns>
        public virtual IEnumerable<IComputeNode> GetChildren(object context = null)
        {
            return Enumerable.Empty<ComputeNode>();
        }
        
        protected virtual Dictionary<string, string> BuildTemplateMap()
        {
            var declarationBuilder = new StringBuilder();
            _declarations.ForEach(declaration => declarationBuilder.AppendLine(declaration));
            
            var groupDeclarationBuilder = new StringBuilder();
            _groupDeclarations.ForEach(declaration => groupDeclarationBuilder.AppendLine(declaration));
            
            var structBuilder = new StringBuilder();
            _structs.ForEach(gpuStruct => structBuilder.AppendLine(gpuStruct));
            
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
                {"functions", functionBuilder.ToString()},
                {"groupDeclarations", groupDeclarationBuilder.ToString()}
            };
        }
        
        protected virtual void HandleDeclaration(FieldDeclaration theDeclaration, bool theIsComputeShader)
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

        protected virtual void HandleFunction(KeyValuePair<string,string> theKeyFunction)
        {
            if(!_functionMap.ContainsKey(theKeyFunction.Key))_functionMap.Add(theKeyFunction.Key, theKeyFunction.Value);
        }
        
        private void HandleShader(ShaderGeneratorContext theContext,bool theIsComputeShader, IDictionary<string,AbstractShaderNode> theShaderInputs, out string theSource, out string theStreams, out string theDefinedStreams)
        {
            theShaderInputs.ForEach(kv =>
            {
                kv.Value?.SetHashCodes(theContext);
            });
            
            var streamBuilder = new StringBuilder();
            var streamDeclareBuilder = new StringBuilder();
            
            theShaderInputs.ForEach(kv =>
            {
                kv.Value?.DeclarationList().ForEach(declaration => HandleDeclaration(declaration, theIsComputeShader));
                kv.Value?.StructList().ForEach(value => _structs.Add(value));
                kv.Value?.MixinList().ForEach(value => _mixins.Add(value));
                kv.Value?.CompositionList().ForEach(value => _compositions.Add(value));
                kv.Value?.FunctionMap().ForEach(HandleFunction);

                streamBuilder.AppendLine("        streams." + kv.Key + " = " + kv.Value.ID+";");
                if (_definedStreams.Contains(kv.Key)) return;

                streamDeclareBuilder.AppendLine("    stream " + TypeHelpers.GetGpuTypeByValue(kv.Value) + " " + kv.Key + ";");
            });

            var drawShaderNode = new DrawShaderNode(theShaderInputs) {HashCode = theContext.GetAndIncIDCount()};

            theSource = drawShaderNode.BuildSourceCode();
            theStreams = streamBuilder.ToString();
            theDefinedStreams = streamDeclareBuilder.ToString();
        }
        
        protected virtual string CheckCode(string theCode)
        {
            if (_isCompute) return theCode;
            return theCode.Replace("RWStructuredBuffer", "StructuredBuffer");
        }

        private ShaderSource _source = null;

        public ShaderSource GenerateShaderSource(ShaderGeneratorContext theContext, MaterialComputeColorKeys baseKeys)
        {
            if (_source != null) return _source;
            var sourceStream = new Dictionary<string,(string source, string stream)>();
            var streamDefinesBuilder = new StringBuilder();
            _inputs.ForEach(input =>
            {
                HandleShader(theContext, _isComputeShader, input.Value, out var source, out var stream, out var streamDefines);
                sourceStream.Add(input.Key,(source,stream));
                streamDefinesBuilder.AppendLine(streamDefines);
            });

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
            ShaderName = "Shader_" + Math.Abs(ShaderCode.GetHashCode());
            ShaderCode = ShaderNodesUtil.Evaluate(ShaderCode, new Dictionary<string, string>{{"shaderID",ShaderName}});

            ShaderNodesUtil.AddShaderSource(_game, ShaderName, ShaderCode, "shaders\\" + ShaderName + ".sdsl");
            _parameters = theContext.Parameters;
            _inputs.ForEach(shaderInputs =>
            {
                shaderInputs.Value.ForEach(gpuValue =>
                {
                    gpuValue.Value.InputList().ForEach(input => input.AddParameters(_parameters));
                });
            });//input.ParentNode.SetParameters(_parameters)
            
            _source = new ShaderClassSource(ShaderName);
            return _source;
        }
    }
}
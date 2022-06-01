using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using Stride.Engine;
using Stride.Rendering.Materials;

namespace Fuse
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
            return TypeHelpers.GetGpuTypeForType<float>();;
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

    public abstract class AbstractShader
    {
        public string ShaderCode { get; private set; }

        public string ShaderName { get; private set; }

        private readonly List<string> _definedStreams;

        protected readonly IDictionary<string, IDictionary<string, AbstractShaderNode>> Inputs;
        
        protected readonly HashSet<string>Declarations = new HashSet<string>();
        private readonly HashSet<string>_structs = new HashSet<string>();
        private readonly HashSet<string>_mixins = new HashSet<string>();
        private readonly HashSet<string>_compositions = new HashSet<string>();
        private readonly Dictionary<string, string> _functionMap = new Dictionary<string, string>();

        private readonly bool _isComputeShader;
        private readonly string _sourceTemplate;
        protected readonly Game Game;
            
        protected AbstractShader(Game theGame, bool theIsComputeShader, IDictionary<string,IDictionary<string,AbstractShaderNode>> theInputs, List<string> theDefinedStreams, string theSource)
        {
            Inputs = theInputs;
            _definedStreams = theDefinedStreams;
            _isComputeShader = theIsComputeShader;
            _sourceTemplate = theSource;
            Game = theGame;

            /*
            // ReSharper disable once VirtualMemberCallInConstructor
            ShaderCode = ShaderNodesUtil.Evaluate(theSource, templateMap);
            // ReSharper disable once VirtualMemberCallInConstructor
            ShaderCode = CheckCode(ShaderCode);
            ShaderName = "Shader_" + Math.Abs(ShaderCode.GetHashCode());
            ShaderCode = ShaderNodesUtil.Evaluate(ShaderCode, new Dictionary<string, string>{{"shaderID",ShaderName}});
            */
        }
        
        protected virtual Dictionary<string,string> CustomTemplates ()
        {
            return new Dictionary<string, string>();
        }

        protected virtual void GenerateSourceCode(ShaderGeneratorContext theContext)
        {
            var sourceStream = new Dictionary<string,(string source, string stream)>();
            var streamDefinesBuilder = new StringBuilder();
            Inputs.ForEach(input =>
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
        }

        protected virtual Dictionary<string, string> BuildTemplateMap()
        {
            var declarationBuilder = new StringBuilder();
            Declarations.ForEach(declaration => declarationBuilder.AppendLine(declaration));
            
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
                {"functions", functionBuilder.ToString()}
            };
        }

        protected virtual string CheckCode(string theCode)
        {
            return theCode;
        }

        protected virtual void HandleDeclaration(FieldDeclaration theDeclaration, bool theIsComputeShader)
        {
            Declarations.Add(theDeclaration.GetDeclaration(theIsComputeShader));
        }
        
        protected virtual void HandleFunction(KeyValuePair<string,string> theKeyFunction)
        {
            if(!_functionMap.ContainsKey(theKeyFunction.Key))_functionMap.Add(theKeyFunction.Key, theKeyFunction.Value);
        }

        private void HandleShader(ShaderGeneratorContext theContext,bool theIsComputeShader, IDictionary<string,AbstractShaderNode> theShaderInputs, out string theSource, out string theStreams, out string theDefinedStreams)
        {
            theShaderInputs.ForEach(kv =>
            {
                if (kv.Value != null) kv.Value.HashCode = theContext.GetAndIncIDCount();
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
    }
    
    public class DrawShader : AbstractShader
    {
        private static readonly List<string> DefinedStreams = new List<string>()
        {
            "ShadingPosition",
            
            "Color",
            
            "ColorTarget",
            "ColorTarget1",
            "ColorTarget2",
            "ColorTarget3",
            "ColorTarget4",
            "ColorTarget5",
            "ColorTarget6",
            "ColorTarget7",
            
            "TexCoord",
            "TexCoord1",
            "TexCoord2",
            "TexCoord3",
            "TexCoord4",
            "TexCoord5",
            "TexCoord6",
            "TexCoord7",
            "TexCoord8",
            "TexCoord9",
        };
        //VS_PS_Base
        private const string DrawShaderSource = @"shader ${shaderID} : VS_PS_Base, ColorBase, Texturing${mixins}
{
    cbuffer Inputs{
${declarations}
    }

${structs}

${functions}
${streamDeclaration}

    stage override void VSMain()
    {
${sourceVS}
${streamsVS}
    }

    stage override void PSMain()
    {
${sourcePS}
${streamsPS}
    }
};";

        
        public DrawShader(
            Game theGame,
            IDictionary<string,AbstractShaderNode> theVertexInputs, 
            IDictionary<string,AbstractShaderNode> thePixelInputs, 
            List<string> theDefinedStreams = null, 
            string theTemplate = DrawShaderSource
            ) : base(theGame,
            false,
            new Dictionary<string, IDictionary<string, AbstractShaderNode>>
            {
                
                {"VS", theVertexInputs},
                {"PS", thePixelInputs}
            },
            GetDefinedStreams(theDefinedStreams),
            theTemplate)
        {
            
        }

        protected override string CheckCode(string theCode)
        {
            return theCode.Replace("RWStructuredBuffer", "StructuredBuffer");
        }

        private static List<string> GetDefinedStreams(List<string> theDefinedStreams)
        {
            if (theDefinedStreams == null) return DefinedStreams;
            var result = new List<string>();
            result.AddRange(theDefinedStreams);
            result.AddRange(DefinedStreams);
            return result;
        }
    }
    
    public class ComputeShader : AbstractShader
    {
        private const string ComputeShaderSource = @"shader ${shaderID} : ComputeShaderBase${mixins}
{
${declarations}

${functions}

    override void Compute()
    {
        ${sourceCS}
    }
};";
        public ComputeShader(Game theGame, IDictionary<string,AbstractShaderNode> theComputeInputs) : base(theGame,
            true,
            new Dictionary<string, IDictionary<string, AbstractShaderNode>>
            {
                {"CS", theComputeInputs}
            },
            new List<string>(),
            ComputeShaderSource)
        {
            
        }
    }
}
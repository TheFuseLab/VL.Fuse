using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core.Extensions;

namespace Fuse
{

    public class DrawShaderNode : AbstractShaderNode
    {
        private GpuValue<float> Output { get; set; }
        
        public DrawShaderNode(IDictionary<string,AbstractGpuValue> theVertexInputs) : base("drawShader")
        {
            Output = new GpuValue<float>("output") {ParentNode = this};
            Ins = theVertexInputs.Values;
            
        }

        protected override string SourceTemplate()
        {
            return "";
        }

        public override AbstractGpuValue AbstractOutput()
        {
            return Output;
        }
    }

    public abstract class AbstractShader
    {
        public string ShaderCode { get; }

        public string ShaderName { get; protected set; }

        private readonly List<string> _definedStreams;

        public AbstractShader(IDictionary<string,IDictionary<string,AbstractGpuValue>> theInputs, List<string> theDefinedStreams, Dictionary<string,string> theCustomTemplate)
        {
            _definedStreams = theDefinedStreams;
            var declarations = new HashSet<string>();
            var structs = new HashSet<string>();
            var mixins = new HashSet<string>();
            var functionMap = new Dictionary<string, string>();

            var sourceStream = new Dictionary<string,(string source, string stream)>();
            var streamDefinesBuilder = new StringBuilder();
            theInputs.ForEach(input =>
            {
                HandleShader(input.Value, declarations, structs, mixins, functionMap, out var source, out var stream, out var streamDefines);
                sourceStream.Add(input.Key,(source,stream));
                streamDefinesBuilder.AppendLine(streamDefines);
            });
            
            var declarationBuilder = new StringBuilder();
            declarations.ForEach(declaration => declarationBuilder.AppendLine(declaration));
            
            var structBuilder = new StringBuilder();
            structs.ForEach(gpuStruct => structBuilder.AppendLine(gpuStruct));
            
            var mixinBuilder = new StringBuilder();
            mixins.ForEach(mixin => mixinBuilder.Append(", " + mixin));
            
            var functionBuilder = new StringBuilder();
            functionMap?.ForEach(kv => functionBuilder.AppendLine(kv.Value));

            var templateMap = new Dictionary<string, string>
            {
                {"mixins", mixinBuilder.ToString()},
                {"declarations", declarationBuilder.ToString()},
                {"structs", structBuilder.ToString()},
                {"functions", functionBuilder.ToString()},
                {"streamDeclaration",streamDefinesBuilder.ToString()}
            };
            theCustomTemplate.ForEach(kv =>
            {
                templateMap.Add(kv.Key,kv.Value);
            });
            
            sourceStream.ForEach(kv =>
            {
                templateMap.Add("source" + kv.Key,kv.Value.source);
                templateMap.Add("streams" + kv.Key,kv.Value.stream);
            });
            
            // ReSharper disable once VirtualMemberCallInConstructor
            ShaderCode = ShaderNodesUtil.Evaluate(Source(), templateMap);
            ShaderName = "Shader_" + Math.Abs(ShaderCode.GetHashCode());
            ShaderCode = ShaderNodesUtil.Evaluate(ShaderCode, new Dictionary<string, string>{{"shaderID",ShaderName}});
        }

        protected abstract string Source();
        
        protected void HandleShader(IDictionary<string,AbstractGpuValue> theShaderInputs, ISet<string> theDeclarations,ISet<string> theStructs, ISet<string> theMixins, Dictionary<string, string> theFunctions, out string theSource, out string theStreams, out string theDefinedStreams)
        {
            var streamBuilder = new StringBuilder();
            var streamDeclareBuilder = new StringBuilder();
            theShaderInputs.ForEach(kv =>
            {
                kv.Value?.ParentNode.DeclarationList().ForEach(declaration => theDeclarations.Add(declaration));
                kv.Value?.ParentNode.StructList().ForEach(gpuStruct => theStructs.Add(gpuStruct));
                kv.Value?.ParentNode.MixinList().ForEach(mixin => theMixins.Add(mixin));
                kv.Value?.ParentNode.FunctionMap().ForEach(keyFunction => {if(!theFunctions.ContainsKey(keyFunction.Key))theFunctions.Add(keyFunction.Key, keyFunction.Value);});

                streamBuilder.AppendLine("        streams." + kv.Key + " = " + kv.Value.ID+";");
                if (_definedStreams.Contains(kv.Key)) return;

                streamDeclareBuilder.AppendLine("    stream " + TypeHelpers.GetGpuTypeByValue(kv.Value) + " " + kv.Key + ";");
            });

            theSource = new DrawShaderNode(theShaderInputs).BuildSourceCode();
            theStreams = streamBuilder.ToString();
            theDefinedStreams = streamDeclareBuilder.ToString();
        }
    }

    public enum GeometryShaderPrimitiveType
    {
        Point,
        Line,
        Triangle,
        Lineadj,
        Triangleadj
    }

    public enum StreamOutputType
    {
        Point,
        Line,
        Triangle
    }
    
    
    
    public class DrawShader : AbstractShader
    {
        private static readonly List<string> DefinedStreams = new List<string>()
        {
            "ShadingPosition",
            
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
        private const string DrawShaderSource = @"shader ${shaderID} : ${BaseShader}, Texturing${mixins}
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


        public DrawShader(IDictionary<string,AbstractGpuValue> theVertexInputs, IDictionary<string,AbstractGpuValue> thePixelInputs, List<string> theDefinedStreams = null, string theBaseShader = "VS_PS_Base") : base(
            new Dictionary<string, IDictionary<string, AbstractGpuValue>>
            {
                {"VS", theVertexInputs},
                {"PS", thePixelInputs}
            },
            GetDefinedStreams(theDefinedStreams),
            new Dictionary<string, string>()
            {
                {"BaseShader",theBaseShader}
            })
        {
            
        }

        private static List<string> GetDefinedStreams(List<string> theDefinedStreams)
        {
            if (theDefinedStreams == null) return DefinedStreams;
            var result = new List<string>();
            result.AddRange(theDefinedStreams);
            result.AddRange(DefinedStreams);
            return result;
        }

        protected override string Source()
        {
            return DrawShaderSource;
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
        public ComputeShader(IDictionary<string,AbstractGpuValue> theComputeInputs) : base(
            new Dictionary<string, IDictionary<string, AbstractGpuValue>>
            {
                {"CS", theComputeInputs}
            },
            new List<string>(),
            new Dictionary<string, string>())
        {
            
        }

        protected override string Source()
        {
            return ComputeShaderSource;
        }
    }
}
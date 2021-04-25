using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
//using Stride.Core.Extensions;
using VL.Lib.Adaptive;
using VL.Lib.Collections;

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
        public string ShaderCode { get; protected set; }

        public string ShaderName { get; protected set; }

        public AbstractShader(IDictionary<string,IDictionary<string,AbstractGpuValue>> theInputs)
        {
            
            var declarations = new HashSet<string>();
            var structs = new HashSet<string>();
            var mixins = new HashSet<string>();
            var functionMap = new Dictionary<string, string>();

            var sourceStream = new Dictionary<string,(string source, string stream)>();
            theInputs.ForEach(input =>
            {
                HandleShader(input.Value, declarations, structs, mixins, functionMap, out var source, out var stream);
                sourceStream.Add(input.Key,(source,stream));
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
                {"functions", functionBuilder.ToString()}
            };
            
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

        public abstract string Source();
        
        protected static void HandleShader(IDictionary<string,AbstractGpuValue> theShaderInputs, ISet<string> theDeclarations,ISet<string> theStructs, ISet<string> theMixins, Dictionary<string, string> theFunctions, out string theSource, out string theStreams)
        {
            var streamBuilder = new StringBuilder();
            theShaderInputs.ForEach(kv =>
            {
                kv.Value?.ParentNode.DeclarationList().ForEach(declaration => theDeclarations.Add(declaration));
                kv.Value?.ParentNode.StructList().ForEach(gpuStruct => theStructs.Add(gpuStruct));
                kv.Value?.ParentNode.MixinList().ForEach(mixin => theMixins.Add(mixin));
                kv.Value?.ParentNode.FunctionMap().ForEach(keyFunction => {if(!theFunctions.ContainsKey(keyFunction.Key))theFunctions.Add(keyFunction.Key, keyFunction.Value);});

                streamBuilder.AppendLine("        streams." + kv.Key + " = " + kv.Value.ID+";");
            });

            theSource = new DrawShaderNode(theShaderInputs).BuildSourceCode();
            theStreams = streamBuilder.ToString();
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
        private const string DrawShaderSource = @"shader ${shaderID} : VS_PS_Base, Texturing${mixins}
{
    cbuffer Inputs{
${declarations}
    }

${structs}

${functions}

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


        private const string GeometryShaderSource = @"[maxvertexcount(${maxVertexCount})]
    stage void GSMain( ${primitiveType} Input input[1], inout ${streamType}<Output> outputStream)
    {
        streams = input[0];

        ${sourceGS}
        for(int i=0; i<4; i++)
        {
            streams.TexCoord  = QuadUV[i].xy;
            
            float4 posView = mul(streams.PositionWS, WorldView);
            posView.xyz += QuadPositions[i].xyz * ParticleSize;
            streams.ShadingPosition = mul(posView, Projection);
            
            outputStream.Append(streams);
        }
       
    }";

        

        public DrawShader(IDictionary<string,AbstractGpuValue> theVertexInputs, IDictionary<string,AbstractGpuValue> thePixelInputs) : base(
            new Dictionary<string, IDictionary<string, AbstractGpuValue>>
            {
                {"VS", theVertexInputs},
                {"PS", thePixelInputs}
            })
        {
            
        }


        public override string Source()
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
            })
        {
            
        }
        
        public override string Source()
        {
            return ComputeShaderSource;
        }
    }
}
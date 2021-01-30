using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Core.Extensions;
using VL.Lib.Adaptive;
using VL.Lib.Collections;

namespace Fuse
{

    public class DrawShaderNode : AbstractShaderNode
    {
        public  GpuValue<float> Output { get; protected set; }
        
        public DrawShaderNode(IDictionary<string,AbstractGpuValue> theVertexInputs) : base("drawShader")
        {
            Output = new GpuValue<float>("output");
            Setup(
                "", 
                theVertexInputs.Values
            );
        }
        
        protected void Setup(string theSourceCode, IEnumerable<AbstractGpuValue> theIns, IDictionary<string, string> theCustomValues = null)
        {
            var abstractGpuValues = theIns.ToList();
            _sourceCode = GenerateSource(theSourceCode, abstractGpuValues, theCustomValues);
            Ins = abstractGpuValues;
            Output.ParentNode = this;
        }
    }
    
    public class DrawShader
    {
        private const string Source = @"shader ${shaderID} : VS_PS_Base, Texturing${mixins}
{
    cbuffer Inputs{
${declarations}
    }

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

        public string ShaderCode { get; }

        public string ShaderName { get; }

        public DrawShader(IDictionary<string,AbstractGpuValue> theVertexInputs, IDictionary<string,AbstractGpuValue> thePixelInputs)
        {
            var declarations = new HashSet<string>();
            var mixins = new HashSet<string>();
            var functionMap = new Dictionary<string, string>();

            HandleShader(theVertexInputs, declarations, mixins, functionMap, out var sourceVS, out var streamsVS);
            HandleShader(thePixelInputs, declarations, mixins, functionMap, out var sourcePS, out var streamsPS);
            
            var declarationBuilder = new StringBuilder();
            declarations.ForEach(declaration => declarationBuilder.AppendLine(declaration));
            
            var mixinBuilder = new StringBuilder();
            mixins.ForEach(mixin => mixinBuilder.Append(", " + mixin));
            
            var functionBuilder = new StringBuilder();
            functionMap?.ForEach(kv => functionBuilder.AppendLine(kv.Value));

            var templateMap = new Dictionary<string, string>
            {
                {"mixins", mixinBuilder.ToString()},
                {"declarations", declarationBuilder.ToString()},
                {"functions", functionBuilder.ToString()},
                {"sourceVS", sourceVS},
                {"streamsVS", streamsVS},
                {"sourcePS", sourcePS},
                {"streamsPS", streamsPS}
            };
            
            ShaderCode = ShaderTemplateEvaluator.Evaluate(Source, templateMap);
            ShaderName = "Shader_" + Math.Abs(ShaderCode.GetHashCode());
            ShaderCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>{{"shaderID",ShaderName}});
        }

        private static void HandleShader(IDictionary<string,AbstractGpuValue> theShaderInputs, ISet<string> theDeclarations, ISet<string> theMixins, Dictionary<string, string> theFunctions, out string theSource, out string theStreams)
        {
            var streamBuilder = new StringBuilder();
            theShaderInputs.ForEach(kv =>
            {
                kv.Value?.ParentNode.DeclarationList().ForEach(declaration => theDeclarations.Add(declaration));
                kv.Value?.ParentNode.MixinList().ForEach(mixin => theMixins.Add(mixin));
                kv.Value?.ParentNode.FunctionMap().ForEach(keyFunction => {if(!theFunctions.ContainsKey(keyFunction.Key))theFunctions.Add(keyFunction.Key, keyFunction.Value);});

                streamBuilder.AppendLine("        streams." + kv.Key + " = " + kv.Value.ID+";");
            });

            theSource = new DrawShaderNode(theShaderInputs).BuildSourceCode();
            theStreams = streamBuilder.ToString();
        }
    }
}
﻿using System;
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

        public string ShaderName { get; }

        private readonly List<string> _definedStreams;

        protected readonly IDictionary<string, IDictionary<string, AbstractGpuValue>> Inputs;
        
        protected readonly HashSet<string>Declarations = new HashSet<string>();
        protected readonly HashSet<string>Structs = new HashSet<string>();
        protected readonly HashSet<string>Mixins = new HashSet<string>();
        protected readonly Dictionary<string, string> FunctionMap = new Dictionary<string, string>();

        protected AbstractShader(bool theIsComputeShader, IDictionary<string,IDictionary<string,AbstractGpuValue>> theInputs, List<string> theDefinedStreams, Dictionary<string,string> theCustomTemplate, string theSource)
        {
            Inputs = theInputs;
            _definedStreams = theDefinedStreams;

            //ShaderNodesUtil.ResetID();

            var sourceStream = new Dictionary<string,(string source, string stream)>();
            var streamDefinesBuilder = new StringBuilder();
            theInputs.ForEach(input =>
            {
                HandleShader(theIsComputeShader, input.Value, out var source, out var stream, out var streamDefines);
                sourceStream.Add(input.Key,(source,stream));
                streamDefinesBuilder.AppendLine(streamDefines);
            });

            var templateMap = BuildTemplateMap();
            templateMap["streamDeclaration"] = streamDefinesBuilder.ToString();
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
            ShaderCode = ShaderNodesUtil.Evaluate(theSource, templateMap);
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
            Structs.ForEach(gpuStruct => structBuilder.AppendLine(gpuStruct));
            
            var mixinBuilder = new StringBuilder();
            Mixins.ForEach(mixin => mixinBuilder.Append(", " + mixin));
            
            var functionBuilder = new StringBuilder();
            FunctionMap?.ForEach(kv => functionBuilder.AppendLine(kv.Value));

            return new Dictionary<string, string>
            {
                {"mixins", mixinBuilder.ToString()},
                {"declarations", declarationBuilder.ToString()},
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
        
        protected virtual void HandleStruct(string theStruct)
        {
            Structs.Add(theStruct);
        }
        
        protected virtual void HandleMixin(string theMixin)
        {
            Mixins.Add(theMixin);
        }
        
        protected virtual void HandleFunction(KeyValuePair<string,string> theKeyFunction)
        {
            if(!FunctionMap.ContainsKey(theKeyFunction.Key))FunctionMap.Add(theKeyFunction.Key, theKeyFunction.Value);
        }

        private void HandleShader(bool theIsComputeShader, IDictionary<string,AbstractGpuValue> theShaderInputs, out string theSource, out string theStreams, out string theDefinedStreams)
        {
            var streamBuilder = new StringBuilder();
            var streamDeclareBuilder = new StringBuilder();
            theShaderInputs.ForEach(kv =>
            {
                kv.Value?.ParentNode?.DeclarationList().ForEach(declaration => HandleDeclaration(declaration, theIsComputeShader));
                kv.Value?.ParentNode?.StructList().ForEach(HandleStruct);
                kv.Value?.ParentNode?.MixinList().ForEach(HandleMixin);
                kv.Value?.ParentNode?.FunctionMap().ForEach(HandleFunction);

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
            IDictionary<string,AbstractGpuValue> theVertexInputs, 
            IDictionary<string,AbstractGpuValue> thePixelInputs, 
            List<string> theDefinedStreams = null, 
            string theTemplate = DrawShaderSource
            ) : base(
            false,
            new Dictionary<string, IDictionary<string, AbstractGpuValue>>
            {
                
                {"VS", theVertexInputs},
                {"PS", thePixelInputs}
            },
            GetDefinedStreams(theDefinedStreams),
            new Dictionary<string, string>(),
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
        public ComputeShader(IDictionary<string,AbstractGpuValue> theComputeInputs) : base(
            true,
            new Dictionary<string, IDictionary<string, AbstractGpuValue>>
            {
                {"CS", theComputeInputs}
            },
            new List<string>(),
            new Dictionary<string, string>(),
            ComputeShaderSource)
        {
            
        }
    }
}
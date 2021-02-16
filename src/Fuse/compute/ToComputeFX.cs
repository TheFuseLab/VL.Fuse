﻿using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core.Extensions;
using Stride.Rendering.Materials;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;
using static VL.Stride.Shaders.ShaderFX.ShaderFXUtils;

namespace Fuse.shaderFXBridge
{
    
    
    public class ToComputeFx : ComputeVoid
    {
        
        public string ShaderCode { get; protected set; }
        public string ShaderName { get; protected set; }

        private const string ComputeShaderSource = @"
shader ${shaderID} : ComputeVoid, ComputeShaderBase${mixins}
{
    cbuffer PerDispatch{
${declarations}
    }

${functions}

    override void Compute()
    {
${sourceCompute}
    }
};";
        
        public ToComputeFx(AbstractGpuValue theCompute)
        {
            var declarations = new HashSet<string>();
            var mixins = new HashSet<string>();
            var functionMap = new Dictionary<string, string>();

            
            HandleShader(theCompute, declarations, mixins, functionMap, out var source);
            
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
                {"sourceCompute", source}
            };
            
            // ReSharper disable once VirtualMemberCallInConstructor
            ShaderCode = ShaderTemplateEvaluator.Evaluate(ComputeShaderSource, templateMap);
            ShaderName = "Shader_" + Math.Abs(ShaderCode.GetHashCode());
            ShaderCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, new Dictionary<string, string>{{"shaderID",ShaderName}});
        }

        protected static void HandleShader(AbstractGpuValue theInput, ISet<string> theDeclarations, ISet<string> theMixins, Dictionary<string, string> theFunctions, out string theSource)
        {
            var streamBuilder = new StringBuilder();
           
            theInput.ParentNode.DeclarationList().ForEach(declaration => theDeclarations.Add(declaration));
            theInput.ParentNode.MixinList().ForEach(mixin => theMixins.Add(mixin));
            theInput.ParentNode.FunctionMap().ForEach(keyFunction => {if(!theFunctions.ContainsKey(keyFunction.Key))theFunctions.Add(keyFunction.Key, keyFunction.Value);});

            

            theSource = theInput.ParentNode.BuildSourceCode();
        }

        public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            return new ShaderClassSource(ShaderName);
        }

        
    }
}
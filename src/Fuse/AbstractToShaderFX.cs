using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core.Extensions;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
    public abstract class AbstractToShaderFX<T> : ComputeNode
    {
        public string ShaderCode { get; }
        public string ShaderName { get; }
        
        private AbstractShaderNode _myNode;
        
        public AbstractToShaderFX(GpuValue<T> theCompute)
        {
            _myNode = theCompute.ParentNode;

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
                {"resultName", theCompute.ID},
                {"resultType", TypeHelpers.GetGpuTypeForType<T>()},
                {"shaderType", TypeHelpers.GetShaderTypeForType<T>()},
                {"mixins", mixinBuilder.ToString()},
                {"declarations", declarationBuilder.ToString()},
                {"functions", functionBuilder.ToString()},
                {"sourceCompute", source},
                {"result", theCompute.ID}
            };
            
            // ReSharper disable once VirtualMemberCallInConstructor
            ShaderCode = ShaderNodesUtil.Evaluate(SourceCode(), templateMap);
            ShaderName = "Shader_" + Math.Abs(ShaderCode.GetHashCode());
            ShaderCode = ShaderNodesUtil.Evaluate(ShaderCode, new Dictionary<string, string>{{"shaderID",ShaderName}});
        }

        protected static void HandleShader(AbstractGpuValue theInput, ISet<string> theDeclarations, ISet<string> theMixins, Dictionary<string, string> theFunctions, out string theSource)
        {
            var streamBuilder = new StringBuilder();
           
            theInput.ParentNode.DeclarationList().ForEach(declaration => theDeclarations.Add(declaration));
            theInput.ParentNode.MixinList().ForEach(mixin => theMixins.Add(mixin));
            theInput.ParentNode.FunctionMap().ForEach(keyFunction => {if(!theFunctions.ContainsKey(keyFunction.Key))theFunctions.Add(keyFunction.Key, keyFunction.Value);});
            theSource = new DrawShaderNode(new Dictionary<string, AbstractGpuValue>{{"in", theInput}}).BuildSourceCode();
        }

        public abstract string SourceCode();
        
        ParameterCollection Parameters;

        public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            Parameters = context.Parameters;
            _myNode.InputList().ForEach(input => input.SetParameters(Parameters));
            return new ShaderClassSource(ShaderName);
        }
    }
    
    public class ToComputeFx : AbstractToShaderFX<GpuVoid>  , IComputeVoid
    {
        
        

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


        public ToComputeFx(GpuValue<GpuVoid> theCompute) : base(theCompute)
        {
        }

        public override string SourceCode()
        {
            return ComputeShaderSource;
        }
    }
    
    public class ToShaderFX<T> : AbstractToShaderFX<T>, IComputeValue<T> where T : struct
    {

        private const string ShaderSource = @"
shader ${shaderID} : VS_PS_Base, Compute${shaderType}${mixins}
{
    cbuffer PerMaterial{
${declarations}
    }

${functions}

    override ${resultType} Compute()
    {
${sourceCompute}
        return ${result};
    }
};";


        public ToShaderFX(GpuValue<T> theCompute) : base(theCompute)
        {
        }

        public override string SourceCode()
        {
            return ShaderSource;
        }
    }
}
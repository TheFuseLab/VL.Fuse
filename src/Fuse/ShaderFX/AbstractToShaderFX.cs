using System;
using System.Collections.Generic;
using System.Text;
using Fuse.compute;
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
        
        private readonly AbstractShaderNode _myNode;

        protected AbstractToShaderFX(GpuValue<T> theCompute, string theSource)
        {
            _myNode = theCompute.ParentNode;

            var declarations = new HashSet<string>();
            var structs = new HashSet<string>();
            var mixins = new HashSet<string>();
            var functionMap = new Dictionary<string, string>();
            
            HandleShader(theCompute, declarations, structs, mixins, functionMap, out var source);
            
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
                {"resultName", theCompute.ID},
                {"resultType", TypeHelpers.GetGpuTypeForType<T>()},
                {"shaderType", TypeHelpers.GetSignatureTypeForType<T>()},
                {"mixins", mixinBuilder.ToString()},
                {"declarations", declarationBuilder.ToString()},
                {"structs", structBuilder.ToString()},
                {"functions", functionBuilder.ToString()},
                {"sourceCompute", source},
                {"result", theCompute.ID}
            };
            
            // ReSharper disable once VirtualMemberCallInConstructor
            ShaderCode = ShaderNodesUtil.Evaluate(theSource, templateMap);
            ShaderName = "Shader_" + Math.Abs(ShaderCode.GetHashCode());
            ShaderCode = ShaderNodesUtil.Evaluate(ShaderCode, new Dictionary<string, string>{{"shaderID",ShaderName}});
        }

        private static void HandleShader(AbstractGpuValue theInput, ISet<string> theDeclarations, ISet<string> theStructs, ISet<string> theMixins, Dictionary<string, string> theFunctions, out string theSource)
        {
            var streamBuilder = new StringBuilder();
           
            theInput.ParentNode.DeclarationList().ForEach(declaration => theDeclarations.Add(declaration));
            theInput.ParentNode.StructList().ForEach(gpuStruct => theStructs.Add(gpuStruct));
            theInput.ParentNode.MixinList().ForEach(mixin => theMixins.Add(mixin));
            theInput.ParentNode.FunctionMap().ForEach(keyFunction => {if(!theFunctions.ContainsKey(keyFunction.Key))theFunctions.Add(keyFunction.Key, keyFunction.Value);});
            theSource = new DrawShaderNode(new Dictionary<string, AbstractGpuValue>{{"in", theInput}}).BuildSourceCode();
        }
        
        protected virtual string CheckCode(string theCode)
        {
            return theCode;
        }

        private ParameterCollection _parameters;

        public override ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            _parameters = context.Parameters;
            _myNode.InputList().ForEach(input => input.SetParameters(_parameters));
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

${structs}

${functions}

    override void Compute()
    {
${sourceCompute}
    }
};";


        public ToComputeFx(GpuValue<GpuVoid> theCompute) : base(theCompute, ComputeShaderSource)
        {
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

${structs}

${functions}

    override ${resultType} Compute()
    {
${sourceCompute}
        return ${result};
    }
};";

        public ToShaderFX(GpuValue<T> theCompute, string theShaderSource = ShaderSource) : base(theCompute, theShaderSource)
        {
        }
    }
}
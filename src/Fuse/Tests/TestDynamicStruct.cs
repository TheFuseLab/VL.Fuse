using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Fuse.compute;
using Stride.Core.Extensions;
using Stride.Core.Mathematics;

namespace Fuse.Tests
{
    public class TestDynamicStruct
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
        
        public static void HandleShader(AbstractGpuValue theInput, ISet<string> theDeclarations, ISet<string> theStructs, ISet<string> theMixins, Dictionary<string, string> theFunctions, out string theSource)
        {
            var streamBuilder = new StringBuilder();
           
            theInput.ParentNode.DeclarationList().ForEach(declaration => theDeclarations.Add(declaration.GetDeclaration(false)));
            theInput.ParentNode.StructList().ForEach(gpuStruct => theStructs.Add(gpuStruct));
            theInput.ParentNode.MixinList().ForEach(mixin => theMixins.Add(mixin));
            theInput.ParentNode.FunctionMap().ForEach(keyFunction => {if(!theFunctions.ContainsKey(keyFunction.Key))theFunctions.Add(keyFunction.Key, keyFunction.Value);});
            theSource = new DrawShaderNode(new Dictionary<string, AbstractGpuValue>{{"in", theInput}}).BuildSourceCode();
            
            theSource = "";
        }

        private static string GetShader<T>(GpuValue<T> theCompute)
        {
            
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
            var shaderCode = ShaderNodesUtil.Evaluate(ComputeShaderSource, templateMap);
            var shaderName = "Shader_" + Math.Abs(shaderCode.GetHashCode());
            
            return ShaderNodesUtil.Evaluate(shaderCode, new Dictionary<string, string>{{"shaderID",shaderName}});
        }
        
        public static void TestBooleanOperator()
        {
            var gpuIndex = new GpuInput<int>();
            var gpuValue1 = new GpuInput<float>();
            
            var valVec4 = new GpuValue<Vector4>("valFloat4");
            var gpuStruct = new DynamicStruct(new List<AbstractGpuValue>(){valVec4},"TestStruct");
            var bufferInput = new DynamicStructBufferInput(gpuStruct.Output);

            var gpuStructInstance = new DynamicStructBufferGet(bufferInput.Output, gpuIndex.Output, gpuStruct.Output, null);
            var structValue = new DynamicStructGetAttribute<Vector4>(gpuStructInstance.Output, valVec4);
            Console.WriteLine(structValue.BuildSourceCode());
            Console.WriteLine(GetShader(structValue.Output));
            
        }
    }
}
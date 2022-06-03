using System;
using System.Collections.Generic;
using System.Text;
using Fuse;
using Fuse.compute;
using NUnit.Framework;
using Stride.Core.Mathematics;

namespace PatchTests
{
    public static class TestDynamicStruct
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
        
        public static void HandleShader(AbstractShaderNode theInput, ISet<string> theDeclarations, ISet<string> theStructs, ISet<string> theMixins, Dictionary<string, string> theFunctions, out string theSource)
        {
            var streamBuilder = new StringBuilder();
           
            theInput.DeclarationList().ForEach(declaration => theDeclarations.Add(declaration.GetDeclaration(false)));
            theInput.StructList().ForEach(gpuStruct => theStructs.Add(gpuStruct));
            theInput.MixinList().ForEach(mixin => theMixins.Add(mixin));
            theInput.FunctionMap().ForEach(keyFunction => {if(!theFunctions.ContainsKey(keyFunction.Key))theFunctions.Add(keyFunction.Key, keyFunction.Value);});
            
            theSource = "";
        }

        private static string GetShader<T>(ShaderNode<T> theCompute)
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
        
        [Test]
        public static void TestBooleanOperator()
        {
            var gpuIndex = new GpuInput<int>();
            var gpuValue1 = new GpuInput<float>();
            
            var valVec4 = new ShaderNode<Vector4>("valFloat4");
            var gpuStruct = new DynamicStruct(new List<AbstractShaderNode>(){valVec4},"TestStruct");
            var bufferInput = new DynamicStructBufferInput(gpuStruct);

            var gpuStructInstance = new DynamicStructBufferGet(bufferInput, gpuIndex, gpuStruct, null);
            var structValue = new DynamicStructGetAttribute<Vector4>(gpuStructInstance, valVec4);
            Console.WriteLine(structValue.BuildSourceCode());
            Console.WriteLine(GetShader(structValue));
            
        }
    }
}
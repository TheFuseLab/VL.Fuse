using System.Collections.Generic;
using System.Text;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.ShaderFX
{
    public class ToShaderFX<T> : AbstractToShaderFX<T> where T : struct
    {
//VS_PS_Base, 
        private const string ShaderSource = @"
shader ${shaderID} : VS_PS_Base, Texturing, Compute${shaderType}${mixins}
{
    rgroup PerMaterial{
${groupDeclarations}
    }

    cbuffer PerMaterial{
${declarations}
    }

${structs}

${functions}

    override ${resultType} Compute()
    {
${sourceFX}
        return ${resultFX};
    }
};";

        private readonly bool _isCompute;
        
        protected readonly HashSet<string>GroupDeclarations = new HashSet<string>();
        
        public ToShaderFX(GpuValue<T> theCompute, bool theIsCompute = false, string theShaderSource = ShaderSource) : base(
            new Dictionary<string, IDictionary<string, AbstractGpuValue>> {
                {
                    "FX", new Dictionary<string, AbstractGpuValue>{{"val1", theCompute}}
                }
            }, 
            new Dictionary<string, AbstractGpuValue>{{"FX",theCompute}},
            new List<string>(),
            new Dictionary<string, string>(),
            theShaderSource)
        {
            _isCompute = theIsCompute;
        }
        
        protected override Dictionary<string, string> BuildTemplateMap()
        {
            var templateMap = base.BuildTemplateMap();
            var groupDeclarationBuilder = new StringBuilder();
            GroupDeclarations.ForEach(declaration => groupDeclarationBuilder.AppendLine(declaration));
            templateMap["groupDeclarations"] = groupDeclarationBuilder.ToString();
            return templateMap;
        }
        
        protected override void HandleDeclaration(FieldDeclaration theDeclaration, bool theIsComputeShader)
        {
            if (theDeclaration.IsResource)
            {
                GroupDeclarations.Add(theDeclaration.GetDeclaration(theIsComputeShader));
            }
            else
            {
                Declarations.Add(theDeclaration.GetDeclaration(theIsComputeShader));
            }
        }
        
        protected override string CheckCode(string theCode)
        {
            if (_isCompute) return theCode;
            return theCode.Replace("RWStructuredBuffer", "StructuredBuffer");
        }
    }
}
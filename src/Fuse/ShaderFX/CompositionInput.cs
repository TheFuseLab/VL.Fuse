using System.Collections.Generic;
using NuGet;
using Stride.Rendering.Materials;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.ShaderFX
{

    public class CompositionInput<T> : ShaderNode<T>
    {
        protected const string DeclarationTemplate = @"
        compose Compute${compositionType} ${compositionName};";
        
        public CompositionInput(string theId, IComputeValue<T> theComposition) : base(theId, null)
        {
            
            AddResource(Compositions, this);
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            var result =  new Dictionary<string, string>
            {
                {"compositionType", TypeHelpers.GetSignatureTypeForType<T>()},
                {"compositionName", ID}
            };
            
            result.AddRange(base.CreateTemplateMap());

            return result;
        }
        
        protected override string SourceTemplate()
        {
            return "${resultType} ${resultName} = ${compositionName}.Compute();";
        }
        
        public override void SetHashCodes(ShaderGeneratorContext theContext)
        {
            base.SetHashCodes(theContext);
            ShaderNodesUtil.Evaluate(DeclarationTemplate,CreateTemplateMap());
        }
    }
}
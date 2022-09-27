using System.Collections.Generic;
using NuGet;
using Stride.Rendering.Materials;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.ShaderFX
{

    public class CompositionInput<T> : ShaderNode<T>
    {
        protected const string DeclarationTemplate = @"
        compose Compute${compositionType} ${compositionName};";
        
        public CompositionInput(NodeContext nodeContext, string theId, IComputeValue<T> theComposition) : base(nodeContext, theId, null)
        {
            
            AddProperty(Compositions, this);
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            var result =  new Dictionary<string, string>
            {
                {"compositionType", TypeHelpers.GetSignatureTypeForType<T>()},
                {"compositionName", ID}
            };

            foreach (var template in base.CreateTemplateMap())
            {
                result.Add(template.Key, template.Value);
            }

            return result;
        }
        
        protected override string SourceTemplate()
        {
            return "${resultType} ${resultName} = ${compositionName}.Compute();";
        }
        
        public override void CheckContext(ShaderGeneratorContext theContext)
        {
            base.CheckContext(theContext);
            ShaderNodesUtil.Evaluate(DeclarationTemplate,CreateTemplateMap());
        }
    }
}
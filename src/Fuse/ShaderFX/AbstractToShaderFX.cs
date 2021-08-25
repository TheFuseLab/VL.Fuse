using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

/*
 * var templateMap = new Dictionary<string, string>
            {
                {"resultName", theCompute.ID},
                {"resultType", TypeHelpers.GetGpuTypeForType<T>()},
                {"shaderType", TypeHelpers.GetSignatureTypeForType<T>()},
                {"sourceCompute", source},
                {"result", theCompute.ID}
            };
 */
namespace Fuse.ShaderFX
{
    public abstract class AbstractToShaderFX<T> : AbstractShader, IComputeValue<T>
    {

        protected AbstractToShaderFX(
            IDictionary<string,IDictionary<string,AbstractGpuValue>> theInputs, 
            IDictionary<string,AbstractGpuValue> theResults, 
            List<string> theDefinedStreams, 
            Dictionary<string,string> theCustomTemplate, 
            string theSource) : base(false, theInputs, theDefinedStreams, AppendTemplateValues(theCustomTemplate,theResults), theSource)
        {
        }

        private static Dictionary<string, string> AppendTemplateValues(
            Dictionary<string, string> theTemplateMap,
            IDictionary<string,AbstractGpuValue> theResults)
        {
            theTemplateMap["shaderType"] = TypeHelpers.GetSignatureTypeForType<T>();
            theTemplateMap["resultType"] = TypeHelpers.GetGpuTypeForType<T>();
            theResults.ForEach(kv => theTemplateMap["result" + kv.Key] = kv.Value.ID);
            return theTemplateMap;
        }

        private ParameterCollection _parameters;
        
        /// <summary>
        /// Gets the children.
        /// </summary>
        /// <param name="context">The context to get the children.</param>
        /// <returns>The list of children.</returns>
        public virtual IEnumerable<IComputeNode> GetChildren(object context = null)
        {
            return Enumerable.Empty<ComputeNode>();
        }


        public ShaderSource GenerateShaderSource(ShaderGeneratorContext context, MaterialComputeColorKeys baseKeys)
        {
            _parameters = context.Parameters;
            Inputs.ForEach(shaderInputs =>
            {
                shaderInputs.Value.ForEach(gpuValue =>
                {
                    gpuValue.Value.ParentNode.InputList().ForEach(input => input.AddParameters(_parameters));
                });
            });//input.ParentNode.SetParameters(_parameters)
            return new ShaderClassSource(ShaderName);
        }
    }
}
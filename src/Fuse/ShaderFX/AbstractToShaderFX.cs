using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Stride.Engine;
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

        private readonly Dictionary<string, string> _customTemplate;
        private readonly IDictionary<string, AbstractShaderNode> _results;
        
        protected AbstractToShaderFX(
            Game theGame,
            IDictionary<string,IDictionary<string,AbstractShaderNode>> theInputs, 
            IDictionary<string,AbstractShaderNode> theResults, 
            List<string> theDefinedStreams, 
            Dictionary<string,string> theCustomTemplate, 
            string theSource) : base(theGame, false, theInputs, theDefinedStreams, theSource)
        {
            _customTemplate = theCustomTemplate;
            _results = theResults;
        }

        private static Dictionary<string, string> AppendTemplateValues(
            Dictionary<string, string> theTemplateMap,
            IDictionary<string,AbstractShaderNode> theResults)
        {
            theTemplateMap["shaderType"] = TypeHelpers.GetSignatureTypeForType<T>();
            theTemplateMap["resultType"] = TypeHelpers.GetGpuTypeForType<T>();
            theResults.ForEach(kv => theTemplateMap["result" + kv.Key] = kv.Value.ID);
            return theTemplateMap;
        }
        
        protected override Dictionary<string,string> CustomTemplates ()
        {
            return AppendTemplateValues(_customTemplate,_results);
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
            GenerateSourceCode(context);
            ShaderNodesUtil.AddShaderSource(_game, ShaderName, ShaderCode, "shaders\\" + ShaderName + ".sdsl");
            _parameters = context.Parameters;
            Inputs.ForEach(shaderInputs =>
            {
                shaderInputs.Value.ForEach(gpuValue =>
                {
                    gpuValue.Value.InputList().ForEach(input => input.AddParameters(_parameters));
                });
            });//input.ParentNode.SetParameters(_parameters)
            return new ShaderClassSource(ShaderName);
        }
    }
}
using System.Collections.Generic;

namespace VL.ShaderFXtension
{
    public class IfRegionNode<T> : ShaderNode<T>
    {
        private const string ShaderCode = @"
    ${resultType} ${resultName}; 
    if(${check}){
        ${resultName} = ${inTrue};
    }
        ";
        
        public delegate void IfRegion();
        
        protected IfRegionNode(GpuValue<bool> inCheck, IfRegion theRegion) : base("IfRegion")
        {
            var myKeyMap = new Dictionary<string, string>
            {
                {"resultName", Output.ID},
                {"resultType", TypeHelpers.GetNameForType<T>().ToLower()},
                {"check", inCheck.ID},
               // {"inFalse", inFalse.ID},
               // {"inTrue", inTrue.ID}
            };
            
            //var myIns = new OrderedDictionary<string, AbstractGpuValue>() {{"check", inCheck}, {"inFalse", inFalse}, {"inTrue", inTrue}};
            var sourceCode = ShaderTemplateEvaluator.Evaluate(ShaderCode, myKeyMap);
            //Setup(sourceCode, myIns);
        }
    }
}
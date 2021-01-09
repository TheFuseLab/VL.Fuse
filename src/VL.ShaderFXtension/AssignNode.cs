using System.Collections.Generic;

namespace VL.ShaderFXtension
{
    public class AssignNode<T> : ShaderNode<T>
    {
        public AssignNode(GpuValue<T> theTarget, GpuValue<T> theSource) : base("Assign")
        {
            var myKeyMap = new Dictionary<string, string>
            {
                {"targetName", theTarget.ID},
                {"sourceName", theSource.ID}
            };

            //Output = theTarget;
            var myIns = new OrderedDictionary<string, AbstractGpuValue>() {{"target", theTarget}, {"source", theSource}};
            var sourceCode = ShaderTemplateEvaluator.Evaluate("${targetName} = ${sourceName};", myKeyMap);
            Setup(sourceCode,myIns);
        }
    }
}
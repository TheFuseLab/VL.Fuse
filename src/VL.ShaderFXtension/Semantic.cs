using System.Collections.Generic;

namespace VL.ShaderFXtension
{
    public class Semantic<T> : ShaderNode
    {
        public GpuValue<T> Output { get; }
        
        public Semantic (string theSemantic) : base("Semantic")
        {
            Output = new SemanticValue<T>(theSemantic);
            Setup(
                "", 
                new OrderedDictionary<string, AbstractGpuValue>(), 
                new OrderedDictionary<string, AbstractGpuValue> {{"semantic", Output}}
            );
        }
    }
}
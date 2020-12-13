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
                new Dictionary<string, AbstractGpuValue>(), 
                new Dictionary<string, AbstractGpuValue> {{"semantic", Output}}
            );
        }
    }
}
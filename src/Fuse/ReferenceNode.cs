using System.Collections.Generic;

namespace Fuse
{

    public interface IReferenceNode
    {
        void SetAbstractInput(AbstractGpuValue theIn);
    }
    
    public class ReferenceNode<T> : ShaderNode<T> , IReferenceNode
    {

        private readonly GpuValuePassThrough<T> _myValue;
        
        public ReferenceNode(AbstractGpuValue theIn) : base("Reference")
        {
            Setup(new List<AbstractGpuValue>{theIn});

            
            Output = _myValue = new GpuValuePassThrough<T>(theIn as GpuValue<T>);
            Output.ParentNode = this;
        }

        public void SetInput(GpuValue<T> theIn)
        {
            Setup(new List<AbstractGpuValue>{theIn});

            _myValue.SetInput(theIn);
        }
        
        public void SetAbstractInput(AbstractGpuValue theIn)
        {
            Setup(new List<AbstractGpuValue>{theIn});

            _myValue.SetInput(theIn as GpuValue<T>);
        }

        protected override string SourceTemplate()
        {
            return "";
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
    }
}
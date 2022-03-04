using System.Collections.Generic;

namespace Fuse
{
    public interface IReferenceNode
    {
        void SetAbstractInput(AbstractGpuValue theIn);
    }
    
    public class ReferenceNode<T> : UtilityNode<T> , IReferenceNode
    {

        private readonly GpuValuePassThrough<T> _myValue;
        
        public ReferenceNode(AbstractGpuValue theIn) : base(theIn, "Reference")
        {
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
    }
    
    public class CrossLink<T> : UtilityNode<T>
    {

        public CrossLink(GpuValue<T> theIn) : base(theIn, "CrossLink")
        {
        }
    }
    
    public class Comment<T> : UtilityNode<T>
    {

        private readonly string _comment;
        
        public Comment(GpuValue<T> theIn, string theComment) : base(theIn, "Comment")
        {
            _comment = theComment;
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = @"
        //
        // ${comment}
        //
        ";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"comment", _comment}
            });
        }
    }
    
    public class UtilityNode<T> : ShaderNode<T>
    {
        public UtilityNode(AbstractGpuValue theIn, string theID) : base(theID)
        {
            Setup(new List<AbstractGpuValue>{theIn});

            
            Output = theIn == null ? new GpuValue<T>(""):new GpuValuePassThrough<T>(theIn as GpuValue<T>);
            Output.ParentNode = this;
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
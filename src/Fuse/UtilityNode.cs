using System.Collections;
using System.Collections.Generic;
using Fuse.compute;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
    public class PassThroughNode<T> : ShaderNode<T>
    {
        protected ShaderNode<T> _input;
        public virtual ShaderNode<T> Input { get => _input;
            set
            {
                _input = value ?? Default;
                SetInputs(new List<AbstractShaderNode>{Input});
            }
        }
        
        public PassThroughNode(NodeContext nodeContext, string theName = "PassThrough") : base(nodeContext, theName)
        {
            Default = new ShaderNode<T>(new NodeSubContextFactory(NodeContext).NextSubContext(),"");
            Input = Default;
        }
        
        public PassThroughNode(NodeContext nodeContext, string theName, AbstractShaderNode theValue) : this(nodeContext, theName)
        {
            SetInput(theValue);
        }
        
        protected override string SourceTemplate()
        {
            return "";
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }

        public virtual void SetInput(AbstractShaderNode theValue)
        {
            Input = theValue as ShaderNode<T>;
        }
        public override string ID => _input.ID;
    }

    public class PassVoid : PassThroughNode<GpuVoid>, IComputeVoid
    {
        public PassVoid(NodeContext nodeContext, string theName = "PassThrough") : base(nodeContext, theName)
        {
        }

        public PassVoid(NodeContext nodeContext, string theName, AbstractShaderNode theValue) : base(nodeContext, theName, theValue)
        {
        }
    }

    public class Output<T> : ShaderNode<T>
    {

        private AbstractShaderNode _input;
        public Output(NodeContext nodeContext, ShaderNode<T> theDefault = null) : base(nodeContext, "AddOutput", theDefault)
        {
            
        }

        public void SetInputs(AbstractShaderNode theComputation, ShaderNode<T> theValue)
        {
            _input = theValue;
            SetInputs(new List<AbstractShaderNode>{theComputation, theValue});
        }
        
        public void SetInputsAbstract(AbstractShaderNode theComputation, AbstractShaderNode theValue)
        {
            _input = theValue;
            SetInputs(new List<AbstractShaderNode>{theComputation, theValue});
        }
        
        protected override string SourceTemplate()
        {
            return "";
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }
        
        public override string ID => _input.ID;
    }
    
    public class CrossLink<T> : PassThroughNode<T>
    {

        public CrossLink(NodeContext nodeContext) : base(nodeContext, "CrossLink")
        {
        }
    }
    
    public class Comment<T> : PassThroughNode<T>
    {

        private string _comment;
        
        public Comment(NodeContext nodeContext) : base(nodeContext, "Comment")
        {
            _comment = "";
        }

        public void SetInput(string theComment, ShaderNode<T> theIn)
        {
            _comment = theComment;
            base.SetInput(theIn);
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = @"
        // ${comment}
        ";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"comment", _comment}
            });
        }
    }
    
    public class AddProperty<T> : PassThroughNode<T>
    {
        private readonly string _propertyId;
        public AddProperty(NodeContext nodeContext, string thePropertyId) : base(nodeContext, "AddProperty")
        {
            _propertyId = thePropertyId;
        }

        public void SetProperties(IList theResources)
        {
            AddProperties(_propertyId, theResources);
            CallChangeEvent();
        }
    }
}
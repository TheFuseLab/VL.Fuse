using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Fuse.compute;
using Fuse.function;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
    public class PassThroughNode<T> : ShaderNode<T>
    {
        public ShaderNode<T> Input { get; set; }

        public PassThroughNode(NodeContext nodeContext, string theName = "PassThrough") : base(nodeContext, theName)
        {
            Default = new ShaderNode<T>(new NodeSubContextFactory(NodeContext).NextSubContext(),"PassThroughDefault");
            // ReSharper disable once VirtualMemberCallInConstructor
        }

        public PassThroughNode(NodeContext nodeContext, AbstractShaderNode theValue, string theName = "PassThrough") : base(nodeContext, theName)
        {
            Default = new ShaderNode<T>(new NodeSubContextFactory(NodeContext).NextSubContext(),"PassThroughDefault");
            // ReSharper disable once VirtualMemberCallInConstructor
            Input = theValue as ShaderNode<T>;
            SetInputs(new List<AbstractShaderNode>{Input});
        }
        
        protected override string SourceTemplate()
        {
            return "";
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }

        public override string ID => Input.ID;
        
        public override string TypeName()
        {
            return Input != null ? Input.TypeName() : base.TypeName();
        }
    }
    
    public class PassThroughDelegate<T> : Delegate<T>
    {

        public Delegate<T> Input { get; }

        public PassThroughDelegate(NodeContext nodeContext, string theName, AbstractShaderNode theValue) : base(nodeContext, theValue, null, theName)
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            
            Input = theValue as Delegate<T>;
            SetInputs(new List<AbstractShaderNode>{Input});
        }
        
        protected override string SourceTemplate()
        {
            return "";
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }
        public override string ID => Input.ID;
        
        public override string TypeName()
        {
            return Input != null ? Input.TypeName() : base.TypeName();
        }
        
        public override string FunctionName => Input == null ? "null input" : Input.FunctionName;
    }

    public class PassVoid : PassThroughNode<GpuVoid>, IComputeVoid
    {
        public PassVoid(NodeContext nodeContext, string theName, AbstractShaderNode theValue) : base(nodeContext, theValue, theName)
        {
        }
    }

    public class Output<T> : ShaderNode<T>
    {

        private readonly AbstractShaderNode _input;
        public Output(NodeContext nodeContext, AbstractShaderNode theComputation, AbstractShaderNode theValue, ShaderNode<T> theDefault = null) : base(nodeContext, "AddOutput", theDefault)
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

        private readonly string _comment;
        
        public Comment(NodeContext nodeContext, string theComment, ShaderNode<T> theIn) : base(nodeContext, theIn, "Comment")
        {
            _comment = theComment;
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
    
    public class Do<T> : PassThroughNode<T>
    {

        
        public Do(NodeContext nodeContext, ShaderNode<T> theIn, ShaderNode<GpuVoid> theCommand) : base(nodeContext, theIn,"Do")
        {
            SetInputs(new List<AbstractShaderNode>{theCommand,Input});
        }
    }
    
    public class AddProperty<T> : PassThroughNode<T>
    {
        private readonly string _propertyId;
        public AddProperty(NodeContext nodeContext, ShaderNode<T> theIn, string thePropertyId, IList theResources) : base(nodeContext, theIn, "AddProperty")
        {
            _propertyId = thePropertyId;
            AddProperties(_propertyId, theResources);
        }
    }
    
    public interface IInjectToRegion{}
    
    public class InjectToRegion<T> : PassThroughNode<T>, IInjectToRegion
    {
        public InjectToRegion(NodeContext nodeContext, ShaderNode<T> theNode) : base(nodeContext, theNode, "InjectToRegion")
        {
        }
    }
    
    public class InjectToRegionDelegate<T> : PassThroughDelegate<T>, IInjectToRegion
    {
        public InjectToRegionDelegate(NodeContext nodeContext, Delegate<T> theDelegate) : base(nodeContext, "InjectToRegion",theDelegate)
        {
        }
    }
    
    public class AddToGraph<T>: ShaderNode<T>, IComputeVoid
    {
        private ShaderNode<T> _input;
        public AddToGraph(NodeContext nodeContext, ShaderNode<T> theMain, IEnumerable<AbstractShaderNode> theInputs, string name = "AddToGraph") : base(nodeContext, name)
        {
            NullInputMode = HandleNullInputMode.Remove;
            _input = theMain;
            var ins = new List<AbstractShaderNode> { theMain };
            ins.AddRange(theInputs);
            SetInputs(ins);
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
}
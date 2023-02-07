using System.Collections.Generic;
using Fuse.compute;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
    public class AssignValue<T> : ShaderNode<GpuVoid>, IComputeVoid
    {
        private ShaderNode<T> _target;
        private ShaderNode<T> _source;
        
        public AssignValue(NodeContext nodeContext) : base(nodeContext, "Assign")
        {
        }
        
        public AssignValue(NodeContext nodeContext, ShaderNode<T> theTarget, ShaderNode<T> theSource) : base(nodeContext, "Assign")
        {
            SetInputs(theTarget, theSource);
        }

        public void SetInputs(ShaderNode<T> theTarget, ShaderNode<T> theSource)
        {
            if(_target != null)_target.WriteCounter--;
            _target = theTarget;
            _target.WriteCounter++;
            _source = theSource;
            SetInputs(new List<AbstractShaderNode>{theTarget,theSource});
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }

        protected override string SourceTemplate()
        {
            if(_target == null || _source == null)return "";
            return ShaderNodesUtil.Evaluate("${target} = ${source};", new Dictionary<string, string>()
            {
                {"target", _target.ID}, 
                {"source", _source.ID}
            });
        }
        
        public ShaderNode<T> Value()
        {
            return _target;
        }
    }
    
    public class SetMember<T> : ShaderNode<GpuVoid>, IComputeVoid
    {

        private ShaderNode<T> _object;
        private AbstractShaderNode _value;
        private string _member;
        
        public SetMember(NodeContext nodeContext) : base(nodeContext, "SetMember")
        {
            
        }
        
        public SetMember(NodeContext nodeContext,ShaderNode<T> theObject, string theMember, AbstractShaderNode theValue) : base(nodeContext, "SetMember")
        {
            SetInputs(theObject,theMember,theValue);
        }

        public void SetInputs(ShaderNode<T> theObject, string theMember, AbstractShaderNode theValue)
        {
            _object = theObject;
            _member = theMember;
            _value = theValue;
            SetInputs(new List<AbstractShaderNode>{theObject,theValue});
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${object}.${member} =  ${value};",new Dictionary<string, string> {
                {"object", _object == null ? "" : _object.ID},
                {"value", _value.ID},
                {"member", _member}
            });
        }
        
        public ShaderNode<T> Object()
        {
            return _object;
        }
    }
    
    public class SetItem<TSource, T> : ShaderNode<GpuVoid>, IComputeVoid  where T :struct
    {
        private ShaderNode<TSource> _source;
        private ShaderNode<int> _index;
        private ShaderNode<T> _value;
        
        public SetItem(NodeContext nodeContext) : base( nodeContext, "setItem")
        {
            
        }

        public void SetInputs(ShaderNode<TSource> theSource, ShaderNode<int> theIndex, ShaderNode<T> theValue)
        {
            _source = theSource;
            _index = theIndex;
            _value = theValue;
            
            SetInputs(new List<AbstractShaderNode>{theSource,theIndex,theValue});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${source}[${index}] =  ${value};";
            
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"source", _source.ID},
                {"index", _index.ID},
                {"value", _value.ID}
            });
        }

    }
}
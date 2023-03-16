using System.Collections.Generic;
using Fuse.compute;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
    public class AssignValue<T> : ShaderNode<GpuVoid>, IComputeVoid
    {
        private readonly ShaderNode<T> _target;
        private readonly ShaderNode<T> _source;

        public AssignValue(NodeContext nodeContext, ShaderNode<T> theTarget, ShaderNode<T> theSource) : base(nodeContext, "Assign")
        {
            _target = theTarget;
            _source = theSource;
            SetInputs(new List<AbstractShaderNode>{_target,_source});
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

        private readonly ShaderNode<T> _object;
        private readonly AbstractShaderNode _value;
        private readonly string _member;

        public SetMember(NodeContext nodeContext,ShaderNode<T> theObject, string theMember, AbstractShaderNode theValue) : base(nodeContext, "SetMember")
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
    
    public class SetItem<T, TValue> : ShaderNode<GpuVoid>, IComputeVoid  where TValue :struct
    {
        private readonly ShaderNode<T> _target;
        private readonly ShaderNode<int> _index;
        private readonly ShaderNode<TValue> _value;
        
        public SetItem(NodeContext nodeContext, ShaderNode<T> theTarget, ShaderNode<int> theIndex, ShaderNode<TValue> theValue) : base( nodeContext, "setItem")
        {
            _target = theTarget;
            _index = theIndex;
            _value = theValue;
            
            SetInputs(new List<AbstractShaderNode>{theTarget,theIndex,theValue});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${target}[${index}] =  ${value};";
            
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"target", _target.ID},
                {"index", _index.ID},
                {"value", _value.ID}
            });
        }
        
        public ShaderNode<T> Value()
        {
            return _target;
        }

    }
}
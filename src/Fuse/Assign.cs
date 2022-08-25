using System.Collections.Generic;
using Fuse.compute;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse
{
    public class AssignValue<T> : ShaderNode<GpuVoid>, IComputeVoid
    {
        private readonly ShaderNode<T> _target;
        private readonly ShaderNode<T> _source;
        
        public AssignValue(ShaderNode<T> theTarget, ShaderNode<T> theSource) : base("Assign")
        {
            _target = theTarget;
            _source = theSource;
            SetInputs(new List<AbstractShaderNode>{theTarget,theSource});
        }

        public void SetInputs()
        {
            
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }

        protected override string SourceTemplate()
        {
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
    
    public class AssignValueToMember<T> : ShaderNode<GpuVoid>, IComputeVoid
    {

        private readonly ShaderNode<T> _object;
        private readonly AbstractShaderNode _value;
        private readonly string _member;
        
        public AssignValueToMember(ShaderNode<T> theObject, string theMember, AbstractShaderNode theValue) : base("StructAttribute")
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
}
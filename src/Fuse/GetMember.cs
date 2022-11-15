using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse
{
    public class GetMember<TIn, TOut> : ShaderNode<TOut>
    {

        private ShaderNode<TIn> _input;
        private string _member;
        public GetMember(NodeContext nodeContext, ShaderNode<TOut> theDefault = null) : base(nodeContext, "GetMember", theDefault)
        {
            
        }
        
        public GetMember(
            NodeContext nodeContext,
            ShaderNode<TIn> theInput, 
            string theMember, 
            ShaderNode<TOut> theDefault = null) : base(nodeContext, "GetMember", theDefault)
        {
            SetInput(theMember, theInput);
        }

        public void SetInput(string theMember, ShaderNode<TIn> theInput)
        {
            _member = theMember;
            _input = theInput;
            SetInputs(new List<AbstractShaderNode>{theInput});
        }

        protected override string SourceTemplate()
        {
            return ShaderNodesUtil.Evaluate("${resultType} ${resultName} = ${input}.${member};",new Dictionary<string, string> {
                {"input", _input == null? "" : _input.ID},
                {"member", _member}
            });
        }
        
        
    }

    public enum Reorder3DMode
    {
        XYZ,
        XZY,
        YXZ,
        YZX,
        ZXY,
        ZYX
    }

    public class Reorder3D : GetMember<Vector3, Vector3>
    {
        public Reorder3D(NodeContext nodeContext, ShaderNode<Vector3> theDefault = null) : base(nodeContext, theDefault)
        {
        }

        public Reorder3D(NodeContext nodeContext, ShaderNode<Vector3> theInput, string theMember, ShaderNode<Vector3> theDefault = null) : base(nodeContext, theInput, theMember, theDefault)
        {
        }

        public void SetInput(Reorder3DMode theMember, ShaderNode<Vector3> theInput)
        {
            base.SetInput(theMember.ToString().ToLower(), theInput);
            CallChangeEvent();
        }
    }
}
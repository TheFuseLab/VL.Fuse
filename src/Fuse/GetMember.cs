using System.Collections.Generic;
using Stride.Core.Mathematics;
using VL.Core;

namespace Fuse
{
    public class GetMember<TIn, TOut> : ResultNode<TOut>
    {

        private readonly ShaderNode<TIn> _input;
        private readonly string _member;
     
        
        public GetMember(
            NodeContext nodeContext,
            ShaderNode<TIn> theInput, 
            string theMember, 
            ShaderNode<TOut> theDefault = null) : base(nodeContext, "GetMember", theDefault)
        {
            _member = theMember;
            _input = theInput;
            if(theInput != null && theMember != null)
            {
                Name = theInput.Name + ShaderNodesUtil.FirstLetterToUpper(theMember);
            }
            SetInputs(new List<AbstractShaderNode>{theInput});
        }

        protected override string ImplementationTemplate()
        {
            return ShaderNodesUtil.Evaluate("${input}.${member}", new Dictionary<string, string>
            {
                {"input", _input == null ? "" : _input.ID},
                {"member", _member}
            });
        }
    }
    
    public class GetItem<TIn, TOut> : ResultNode<TOut> 
    {
        private readonly ShaderNode<TIn> _input;
        private readonly ShaderNode<int> _index;
        
        public GetItem(NodeContext nodeContext, ShaderNode<TIn> theInput, ShaderNode<int> theIndex) : base( nodeContext, "GetItem")
        {
            _input = theInput;
            _index = theIndex;

            SetInputs(new List<AbstractShaderNode>{theInput as AbstractShaderNode,theIndex});
            
        }

        protected override string ImplementationTemplate()
        {
            const string shaderCode = "${inputName}[${index}]";
            
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"inputName", _input.ID},
                {"index", _index.ID}
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

        public Reorder3D(NodeContext nodeContext, ShaderNode<Vector3> theInput, Reorder3DMode theMember, ShaderNode<Vector3> theDefault = null) : base(nodeContext, theInput, theMember.ToString().ToLower(), theDefault)
        {
        }
    }
}
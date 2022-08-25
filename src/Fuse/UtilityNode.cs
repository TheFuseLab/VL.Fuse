using System.Collections;
using System.Collections.Generic;

namespace Fuse
{
    public class UtilityNode<T> : ShaderNode<T>
    {
        public UtilityNode(AbstractShaderNode theIn, string theID) : base(theID)
        {
            SetInputs(new List<AbstractShaderNode>{theIn});
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
    
    public class CrossLink<T> : UtilityNode<T>
    {

        public CrossLink(ShaderNode<T> theIn) : base(theIn, "CrossLink")
        {
        }
    }
    
    public class Comment<T> : UtilityNode<T>
    {

        private readonly string _comment;
        
        public Comment(ShaderNode<T> theIn, string theComment) : base(theIn, "Comment")
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
    
    public class AddResource<T> : UtilityNode<T>
    {
        
        public AddResource(ShaderNode<T> theIn, string theResourceId, IList theResources) : base(theIn, "AddResource")
        {
            AddResources(theResourceId, theResources);
        }
    }
}
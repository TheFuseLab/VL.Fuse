using System.Collections.Generic;

namespace Fuse
{
    public class Comment<T> : ShaderNode<T>
    {

        private readonly string _comment;
        
        public Comment(GpuValue<T> theIn, string theComment) : base("Comment")
        {
            Setup(new List<AbstractGpuValue>{theIn});

            _comment = theComment;
            
            Output = theIn == null ? new GpuValue<T>(""):new GpuValuePassThrough<T>(theIn);
            Output.ParentNode = this;
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
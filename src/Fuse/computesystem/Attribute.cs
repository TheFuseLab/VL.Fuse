using System.Collections.Generic;

namespace Fuse.ComputeSystem
{
    public interface IAttribute
    {
        public string Name { get; }
        
        public bool IsBuffered { get; }
        public bool IsDoubleBuffered { get; }
        
        public AbstractShaderNode ShaderNode { get; }
        
        public void SetInput(AbstractShaderNode theNode);

        public List<string> Description { get; }
    }
    
    public class Attribute<T> : PassThroughNode<T>, IAttribute
    {
        
        public bool IsBuffered { get; }
        public bool IsDoubleBuffered { get; }
        
        public AbstractShaderNode ShaderNode { get; }
        public List<string> Description {
            get { 
                var keys = new List<string> { "x", "y", "z", "w"};
                var dimension = TypeHelpers.GetDimension<T>();
                var result = new List<string>();
                if (dimension == 1)
                {
                    result.Add(Name);
                }else{
                    for (var i = 0; i < dimension;i++)
                    {
                        result.Add(Name +"." + keys[i]);
                    }
                }
                return result;
        } }

        public Attribute(string theGroup, string theName, bool theIsBuffered, bool theIsDoubleBuffered) : base(theName)
        {
            IsBuffered = theIsBuffered;
            IsDoubleBuffered = theIsDoubleBuffered;
            ShaderNode = new ShaderNode<T>(theName);
            
            AddResource(theGroup, this);
        }

        public void SetAbstractInput(AbstractShaderNode theNode)
        {
            SetInput(theNode);
        }
    }
}
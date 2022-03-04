using System.Collections;
using System.Collections.Generic;

namespace Fuse
{
    public class AddResourceNode<T> : UtilityNode<T>
    {
        
        public AddResourceNode(GpuValue<T> theIn, string theResourceId, IList theResources) : base(theIn, "AddResource")
        {
            AddResources(theResourceId, theResources);
        }
    }
}
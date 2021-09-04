using System.Collections.Generic;

namespace Fuse.regions
{

    public interface IRegionParameter
    {
        public int ID { get; }
        
        
    }
    
    public class FuseRegion : MultiOutputNode
    {
        private ISet<IRegionParameter> _myRegionParameter = new HashSet<IRegionParameter>();
        
        public FuseRegion(List<AbstractGpuValue> theInputs, List<AbstractGpuValue> theOutputs) : base("region")
        {
            theInputs.ForEach(input =>
            {
                input.ParentNode.ChildrenOfType<IRegionParameter>().ForEach(parameter => _myRegionParameter.Add(parameter));
            });
        
            
        }

        protected override string SourceTemplate()
        {
            throw new System.NotImplementedException();
        }

        public override AbstractGpuValue AbstractOutput()
        {
            throw new System.NotImplementedException();
        }
    }
}
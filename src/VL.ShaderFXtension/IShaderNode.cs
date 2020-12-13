using System.Collections.Generic;
using System.Text;
using VL.Lib.Collections;

namespace VL.ShaderFXtension
{
    public interface IShaderNode: Trees.IReadOnlyTreeNode
    {
        void BuildSource(StringBuilder theBuilder, HashSet<int> theHashs);
        void BuildDeclarations(Dictionary<int,string> theBuilder);
        void GetInputs(HashSet<IGPUInput> theInputs);
        void GetMixins(HashSet<string> theMixins);

        string ID { get;  }
    }
}
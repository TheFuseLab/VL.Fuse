using System.Collections.Generic;
using System.Text;
using VL.Lib.Collections;

namespace VL.ShaderFXtension
{
    public interface IShaderNode: Trees.IReadOnlyTreeNode
    {
        string SourceCode();

        void BuildSource(StringBuilder theBuilder, HashSet<int> theHashs);
        void BuildDeclarations(Dictionary<int,string> theBuilder);
        void GetInputs(HashSet<IGPUInput> theInputs);
        void GetMixins(HashSet<string> theMixins);
    }
}
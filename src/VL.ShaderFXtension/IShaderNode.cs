using System.Collections.Generic;
using System.Text;
using VL.Lib.Collections;

namespace VL.ShaderFXtension
{
    public interface IShaderNode: Trees.IReadOnlyTreeNode
    {
        string SourceCode();

        void BuildSource(StringBuilder theBuilder);
        void BuildDeclarations(Dictionary<int,string> theBuilder);
    }
}
using System.Collections.Generic;
using System.Text;
using VL.Lib.Collections;

namespace VL.ShaderFXtension
{
    public interface IShaderNode: Trees.IReadOnlyTreeNode
    {

        string ID { get;  }
    }
}
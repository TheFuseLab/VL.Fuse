using System.Collections;
using System.Collections.Generic;
using Stride.Rendering.Materials;

namespace Fuse;

public interface IDelegate
{
    string Name { get; }
    string FunctionName { get; }
        
    IDictionary<string, string> Functions { get; }
        
    Dictionary<string, IList> PropertiesForTree();

    public void CheckContext(ShaderGeneratorContext theContext);
}
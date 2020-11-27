using System.Collections.Generic;
using System.Text.RegularExpressions;
using Stride.Animations;

namespace VL.ShaderFXtension
{
    public class ShaderTemplateEvaluator
    {
        public static string Evaluate(string theShaderTemplate, IDictionary<string,string> theKeys)
        {
            return Regex.Replace(
                theShaderTemplate, 
                @"\$\{(?<key>[^}]+)\}", 
                m => theKeys.ContainsKey(m.Groups["key"].Value) ? theKeys[m.Groups["key"].Value] : m.Value
                );
        }
    }
}
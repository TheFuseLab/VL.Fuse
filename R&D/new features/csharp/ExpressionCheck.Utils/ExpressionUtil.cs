using System.Text.RegularExpressions;

namespace Main;

public class ShaderGraphParser
{
    private string codeSnippet;
    private Dictionary<string, string> variableReplacements = new Dictionary<string, string>();
    private HashSet<string> functionNames = new HashSet<string> { "abs", "clamp", "length" }; // Predefined function names

    public ShaderGraphParser(string code)
    {
        this.codeSnippet = code;
    }

    public void Parse()
    {
        // Regex pattern to match variable declarations and usages, excluding function calls
        string pattern = @"(\bfloat\b|\bvec3\b)\s+(\w+)|\b(\w+)\b(?!\s*\()";
        MatchCollection matches = Regex.Matches(codeSnippet, pattern);

        foreach (Match match in matches)
        {
            if (match.Groups[1].Success) // Variable declaration
            {
                string varType = match.Groups[1].Value;
                string varName = match.Groups[2].Value;

                // Generate a unique identifier for the declared variable
                string uniqueName = GenerateUniqueName(varName);
                variableReplacements[varName] = uniqueName;
            }
            else if (match.Groups[3].Success) // Variable usage
            {
                string varName = match.Groups[3].Value;

                if (!functionNames.Contains(varName) && !variableReplacements.ContainsKey(varName)) // Not a function and not already replaced
                {
                    // Generate a unique identifier for the input variable
                    string uniqueName = GenerateUniqueName(varName);
                    variableReplacements[varName] = uniqueName;
                }
            }
        }
    }

    private string GenerateUniqueName(string baseName)
    {
        // Simplistic unique identifier generation for demonstration
        return $"{baseName}_{new Random().Next(10000, 99999)}";
    }

    public string GenerateModifiedCode()
    {
        string modifiedCode = codeSnippet;
        // Replace variables with their unique identifiers, carefully handling property access
        foreach (var replacement in variableReplacements)
        {
            // Use a regex pattern that replaces the variable name, ensuring property accesses are correctly associated
            modifiedCode = Regex.Replace(modifiedCode, $@"\b{replacement.Key}\b(?!\.\w+)", replacement.Value);
        }
        return modifiedCode;
    }

    public static ShaderGraphParser ParseExpression(string theExpression)
    {
        var parser = new ShaderGraphParser(theExpression);
        parser.Parse();
        return parser;
    }
}
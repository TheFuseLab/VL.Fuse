using System.Text.RegularExpressions;

namespace Main;

public enum ShaderArgumentModifier
{
    In,
    Out,
    InOut
}

public class ShaderFunctionInfo
{
    public string ReturnType { get; set; }
    public string FunctionName { get; set; }
    public List<ShaderArgument> Arguments { get; set; } = new List<ShaderArgument>();

    public ShaderFunctionInfo(string returnType, string functionName)
    {
        ReturnType = returnType;
        FunctionName = functionName;
    }

    public void AddArgument(string type, string name, ShaderArgumentModifier modifier = ShaderArgumentModifier.In)
    {
        Arguments.Add(new ShaderArgument(type, name, modifier));
    }
}

public class ShaderArgument
{
    public string Type { get; set; }
    public string Name { get; set; }
    public ShaderArgumentModifier Modifier { get; set; } = ShaderArgumentModifier.In; // Default value

    public ShaderArgument(string type, string name, ShaderArgumentModifier modifier = ShaderArgumentModifier.In)
    {
        Type = type;
        Name = name;
        Modifier = modifier;
    }
}

public class ShaderGraphParser
{
    
    public ShaderFunctionInfo ProcessShaderFunction(string shaderFunction)
    {
        // Extract return type
        string returnType = Regex.Match(shaderFunction, @"^\s*\w+").Value.Trim();

        // Extract function name
        string functionName = Regex.Match(shaderFunction, @"\w+\s*\(").Value.Trim('(').Trim();

        // Initialize ShaderFunctionInfo with extracted returnType and functionName
        ShaderFunctionInfo functionInfo = new ShaderFunctionInfo(returnType, functionName);


        // Updated code to extract arguments with modifiers
        string argumentsString = Regex.Match(shaderFunction, @"\(([^)]+)\)").Groups[1].Value;
        var arguments = argumentsString.Split(',');
        foreach (var argument in arguments)
        {
            var parts = Regex.Split(argument.Trim(), @"\s+"); // Split by whitespace, accounting for multiple spaces
            if (parts.Length >= 2)
            {
                ShaderArgumentModifier modifier = ShaderArgumentModifier.In; // Default modifier
                int typeIndex = 0; // Index of the type in parts[]

                // Check for modifier and adjust typeIndex accordingly
                if (parts[0].Equals("in", StringComparison.OrdinalIgnoreCase))
                {
                    modifier = ShaderArgumentModifier.In;
                    typeIndex = 1;
                }
                else if (parts[0].Equals("out", StringComparison.OrdinalIgnoreCase))
                {
                    modifier = ShaderArgumentModifier.Out;
                    typeIndex = 1;
                }
                else if (parts[0].Equals("inout", StringComparison.OrdinalIgnoreCase))
                {
                    modifier = ShaderArgumentModifier.InOut;
                    typeIndex = 1;
                }

                // Assuming the last part is always the name
                var argType = parts[typeIndex];
                string argName = parts[parts.Length - 1];
                functionInfo.AddArgument(argType, argName, modifier); // Updated to include modifier
            }
        }

        return functionInfo;
    }
    
   /*
    * / Example of replacing function name - demonstration purpose
        string newFunctionName = "newFunctionName";
        string newFunctionDeclaration = shaderFunction.Replace(functionName, newFunctionName);
        
        Console.WriteLine($"Modified Function Name: {newFunctionDeclaration}");
    */
    

    public static ShaderFunctionInfo ParseExpression(string theExpression)
    {
        return new ShaderGraphParser().ProcessShaderFunction(theExpression);
    }
}
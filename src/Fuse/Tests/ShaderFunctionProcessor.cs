namespace Fuse.Tests;

using System;
using System.Text.RegularExpressions;

public class ShaderFunctionProcessor
{
    public void ProcessShaderFunction(string shaderFunction)
    {
        // Extract return type
        string returnType = Regex.Match(shaderFunction, @"^\s*\w+").Value.Trim();
        
        // Extract function name
        string functionName = Regex.Match(shaderFunction, @"\w+\s*\(").Value.Trim('(').Trim();
        
        // Extract arguments
        string argumentsString = Regex.Match(shaderFunction, @"\(([^)]+)\)").Groups[1].Value;
        var arguments = argumentsString.Split(',');
        
        Console.WriteLine($"Return Type: {returnType}");
        Console.WriteLine($"Function Name: {functionName}");
        
        Console.WriteLine("Arguments and Their Types:");
        foreach (var argument in arguments)
        {
            var parts = argument.Trim().Split(' ');
            if(parts.Length >= 2) // Simple validation
            {
                string argType = parts[0];
                string argName = parts[1];
                Console.WriteLine($"Type: {argType}, Name: {argName}");
            }
        }
        
        // Example of replacing function name - demonstration purpose
        string newFunctionName = "newFunctionName";
        string newFunctionDeclaration = shaderFunction.Replace(functionName, newFunctionName);
        
        Console.WriteLine($"Modified Function Name: {newFunctionDeclaration}");
    }
}

class Program
{
    static void Main(string[] args)
    {
        string shaderFunction = "float remap(float value, float sourceMin, float sourceMax, float destMin, float destMax){ return (value - sourceMin) / (sourceMax - sourceMin) * (destMax - destMin) + destMin; }";
        
        ShaderFunctionProcessor processor = new ShaderFunctionProcessor();
        processor.ProcessShaderFunction(shaderFunction);
    }
}
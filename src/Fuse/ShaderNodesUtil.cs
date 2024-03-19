﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Fuse.ShaderFX;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders.Compiler;
using Stride.Shaders.Parser;
using VL.Core;
using VL.Stride;
using VL.Stride.Rendering;
using VL.Stride.Rendering.ComputeEffect;
using VL.Stride.Shaders.ShaderFX;
using ServiceRegistry = VL.Core.ServiceRegistry;

namespace Fuse
{
    
    public static class DictionaryExtensions
    {
        public static void ForEach<TKey, TValue>(this Dictionary<TKey, TValue> dict, Action<KeyValuePair<TKey, TValue>> action)
        {
            foreach (var item in dict)
                action(item);
        }
    }
    
    public static class EnumerableExtensionForEach
    {
        public static void ForEach<T>(this IEnumerable<T> list, Action<T> block) {
            foreach (var item in list) {
                block(item);
            }
        }
    }

    public static class ShaderNodesUtil
    {

        public static void SetConsoleOut(string path)
        {
            var filestream = new FileStream(path, FileMode.Create);
            var streamWriter = new StreamWriter(filestream);
            streamWriter.AutoFlush = true;
            Console.SetOut(streamWriter);
            Console.SetError(streamWriter);
        }
        
        public static bool DebugShaderGeneration = false;
        public static bool DebugVisit = false;
        public static bool TimeShaderGeneration = false;

        public static string DebugIdent = ". ";
        /*
        public static bool IsFunctionNode(AbstractShaderNode theNode)
        {
            return theNode.Outs.Select(output => output.GetType()).Any(objectType => objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(Invoke<>));
        }*/

        public static string BuildArguments(IEnumerable<AbstractShaderNode> inputs)
        {
            var stringBuilder = new StringBuilder();
            inputs.ForEach(input =>
            {
                if (input == null) return;
                stringBuilder.Append(input.ID);
                stringBuilder.Append(", ");
            });
            if(stringBuilder.Length > 2)stringBuilder.Remove(stringBuilder.Length - 2, 2);
            return stringBuilder.ToString();
        }

        public static  bool HasNullValue<T>(IEnumerable<T> theSequence)
        {
            return theSequence.Any(value => value == null);
        }

        public static string IndentCode(string theCode)
        {
            var lines = theCode.Split(
                new[] { Environment.NewLine },
                StringSplitOptions.None
            );
            
            var myStringBuilder = new StringBuilder();
            lines.ForEach(line=>myStringBuilder.AppendLine("    " + line));
            return myStringBuilder.ToString();
        }
        
        public static string FirstLetterToUpper(string str)
        {
            if (str == null)
                return null;

            if (str.Length > 1)
                return char.ToUpper(str[0]) + str[1..];

            return str.ToUpper();
        }
        
        public static string FirstLetterToLower(string str)
        {
            if (str == null)
                return null;

            if (str.Length > 1)
                return char.ToLower(str[0]) + str[1..];

            return str.ToLower();
        }

        public static string Evaluate(string theShaderTemplate, IDictionary<string,string> theKeys)
        {
            return Regex.Replace(
                theShaderTemplate, 
                @"\$\{(?<key>[^}]+)\}", 
                m => theKeys.ContainsKey(m.Groups["key"].Value) ? theKeys[m.Groups["key"].Value] : m.Value
            );
        }

        public static string Evaluate(string theShaderTemplate, MatchEvaluator theEvaluator)
        {
            return Regex.Replace(
                theShaderTemplate, 
                @"\$\{(?<key>[^}]+)\}", 
                theEvaluator
            );
        }

        public static string EvaluateIDs(string theShaderTemplate)
        {
            var id = 0;
            var idMap = new Dictionary<string, string>();
            return Regex.Replace(
                theShaderTemplate, 
                @"\$\{#(?<key>[^}]+)\}", 
                m =>
                {
                    var key = m.Groups["key"].Value;
                    if (!idMap.ContainsKey(key))
                    {
                        idMap[key] = id + "";
                        id++;
                    }

                    return idMap[key];
                });
        }
        
        public static string CleanVlClassName(string theVlClassName)
        {
            const string pattern = "_.*";
            const string replacement = "";
            return Regex.Replace(theVlClassName, pattern, replacement);
        }
        
        public static string FixName(string theName)
        {
            return theName.Replace(".","").Replace(" ","");
        }
        
        public static string FormatShaderCode(string shaderCode)
        {
            var formattedCode = new StringBuilder();
            var indentLevel = 0;
            const string indentString = "    "; // You can adjust this to your preferred indentation (e.g., two spaces or a tab)
            var previousLineWasBlank = false;

            using (var reader = new StringReader(shaderCode))
            {
                while (reader.ReadLine() is { } line)
                {
                    line = line.Trim();

                    if (line.Length == 0) // Current line is blank
                    {
                        if (!previousLineWasBlank) // Add a blank line only if the previous line wasn't blank
                        {
                            formattedCode.AppendLine();
                            previousLineWasBlank = true;
                        }
                        continue;
                    }

                    previousLineWasBlank = false; // Reset the flag since the current line is not blank
                    
                    // Decrease indent level if line contains a closing brace
                    if (line.Contains('}')){
                        indentLevel--;
                        indentLevel = System.Math.Max(indentLevel, 0); // Ensure indentLevel is never negative
                    }
                    
                    // Append indented line to the formatted code
                    formattedCode.AppendLine($"{new string(indentString[0], indentLevel * indentString.Length)}{line}");
                    
                    // Increase indent level if line contains an opening brace
                    if (line.Contains('{'))
                        indentLevel++;

                    

                    

                }
            }

            return formattedCode.ToString();
        }


        public static void AddShaderSource(string type, string sourceCode, string sourcePath)
        {
            try
            {
                var game = ServiceRegistry.Current.GetGameProvider().GetHandle().Resource;
                if (game == null) return;

                var effectSystem = game.EffectSystem;
                var compiler = effectSystem.Compiler as EffectCompiler;
                if (compiler is null && effectSystem.Compiler is EffectCompilerCache effectCompilerCache)
                    compiler = typeof(EffectCompilerChain)
                        .GetProperty("Compiler", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?.GetValue(effectCompilerCache) as EffectCompiler;

                if (compiler == null) return;

                var getParserMethod =
                    typeof(EffectCompiler).GetMethod("GetMixinParser", BindingFlags.Instance | BindingFlags.NonPublic);
                if (getParserMethod == null) return;
                if (!(getParserMethod.Invoke(compiler, null) is ShaderMixinParser parser)) return;

                var sourceManager = parser.SourceManager;
                sourceManager.AddShaderSource(type, sourceCode, sourcePath);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        // ReSharper disable once UnusedMember.Global
        // accessed from vl
        public static VLComputeEffectShader RegisterComputeShader<T>( ToComputeFx<T> theComputeFx)
        {
            var game = ServiceRegistry.Current.GetGameProvider().GetHandle().Resource;
            if (game == null) return null;
            
            var shaderGraph = ShaderGraph.BuildFinalShaderGraph(theComputeFx);
            return ShaderGraph.ComposeComputeShader(game.GraphicsDevice, game.Services, shaderGraph);
        }

        class DynamicDrawEffectInstance : DynamicEffectInstance
        {
            public readonly CompositeDisposable Subscriptions = new();

            public DynamicDrawEffectInstance(string effectName, ParameterCollection parameters = null) : base(effectName, parameters)
            {
            }

            protected override void Destroy()
            {
                Subscriptions.Dispose();
                base.Destroy();
            }
        }
        
        
        public static DynamicEffectInstance RegisterDrawShader(ToDrawFX theDrawShader)
        {
            var watch = new Stopwatch();
            
            watch.Start();
            var game = ServiceRegistry.Current.GetGameProvider().GetHandle().Resource;
            if (game == null) return null;
            
            var effectImageShader = new DynamicDrawEffectInstance("ShaderFXGraphEffect");
            var method = typeof(ShaderGraph).GetMethod("NewShaderGeneratorContext", BindingFlags.Static | BindingFlags.NonPublic);
            var context = method?.Invoke(null, new object[]{game.GraphicsDevice, effectImageShader.Parameters, effectImageShader.Subscriptions});
            
            //var context = ShaderGraph.NewShaderGeneratorContext(game.GraphicsDevice, effectImageShader.Parameters, effectImageShader.Subscriptions);
            var key = new MaterialComputeColorKeys(MaterialKeys.DiffuseMap, MaterialKeys.DiffuseValue, Stride.Core.Mathematics.Color.White);
            theDrawShader.GenerateShaderSource((ShaderGeneratorContext) context,key);
            effectImageShader.EffectName = theDrawShader.ShaderName;
            Console.WriteLine($"Register Time: {watch.ElapsedMilliseconds} ms for Shader {theDrawShader.ShaderName}");
            return effectImageShader;
        }

        // ReSharper disable once UnusedMember.Global
        // accessed from vl
        public static ToShaderFX<T> RegisterShaderFX<T>(ShaderNode<T> theGpuValue, bool isCompute = true) where T : struct
        {
            return new ToShaderFX<T>(theGpuValue);
        }

        public static void ListInputs(List<IGpuInput> theInputs, AbstractShaderNode theGpuValue)
        {
            if (theGpuValue == null) return;
            
            theInputs.AddRange(theGpuValue.InputList());
        }

        public static Dictionary<string, List<TProperty>> PropertiesForTree<TProperty>(AbstractShaderNode theNode)
        {
            return theNode.PropertiesForTree<TProperty>();
        }
        
        public static List<TProperty> PropertiesForTreeList<TProperty>(AbstractShaderNode theNode, string theThePropertyId)
        {
            return theNode.PropertyForTree<TProperty>(theThePropertyId);
        }
        
        public static List<TProperty> PropertiesForTreeList<TProperty>(AbstractShaderNode theNode)
        {
            return theNode.PropertiesForTreeList<TProperty>();
        }
        
        static readonly PropertyKey<int> VarIDCounterKey = new PropertyKey<int>("Fuse.FuseIDCounter", typeof(int), DefaultValueMetadata.Static(0, keepDefaultValue: true));

        
        internal static int GetAndIncIDCount(this ShaderGeneratorContext context)
        {
            var result = context.Tags.Get(VarIDCounterKey);
            context.Tags.Set(VarIDCounterKey, result + 1);
            return result;
        }

        public static int Id2=0;

        public static int GetAndIncIDCount3()
        {
            return Id2++;
        }
        
        /**
         * To be checked if hashes are persistent and individual
         * 
         */
        public static int GetStableHashCode(this string str)
        {
            unchecked
            {
                var hash1 = 5381;
                var hash2 = hash1;

                for(var i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i+1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i+1];
                }

                return hash1 + (hash2*1566083941);
            }
        }
        
        public static uint GetHashCode(NodeContext nodeContext)
        {
            unchecked{
                return (uint)GetStableHashCode(nodeContext.Path.ToString());
            }
        }

        public  static Matrix GetParentTransform(RenderDrawContext renderDrawContext)
        {
            return renderDrawContext.RenderContext.Tags.Get(EntityRendererRenderFeature.CurrentParentTransformation);
        }
    }

    public class NodeSubContextFactory
    {
        
        private int _subContextId = 0;

        private readonly int _startIndex = 0;

        private readonly NodeContext _context;

        public NodeSubContextFactory(NodeContext nodeContext, int theStartIndex = 0)
        {
            _context = nodeContext;
            _startIndex = theStartIndex;
        }

        public void Reset()
        {
            _subContextId = _startIndex;
        }
        
        public NodeContext NextSubContext()
        {
            if(_context == null)return NodeContext.CurrentRoot;
            
            var result = _context.CreateSubContext(new UniqueId("Fuse", _subContextId.ToString()));
            _subContextId++;
            return result;
        }
    }
}
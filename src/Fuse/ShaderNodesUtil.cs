using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Fuse.compute;
using Fuse.ShaderFX;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Shaders.Compiler;
using Stride.Shaders.Parser;
using VL.Lib.Collections;
using VL.Stride.Rendering.ComputeEffect;
using VL.Stride.Shaders;
using VL.Stride.Shaders.ShaderFX;

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

    public static class AbstractCreation
    {
        public static AbstractShaderNode CreateAbstract(AbstractShaderNode theValue, Type theType, object[] theArguments )
        {
            var dataType = new[] { theValue.GetType().BaseType.GetGenericArguments()[0]};
            var getType = theType.MakeGenericType(dataType);
            var getInstance = Activator.CreateInstance(getType, theArguments) as AbstractShaderNode;
            return getInstance;
        }
        
        public static AbstractShaderNode AbstractComputeTextureGet(ShaderNode<Texture> theTexture, AbstractShaderNode theIndex, AbstractShaderNode theValue)
        {
            return CreateAbstract(theValue, typeof(PassThroughNode<>), new object[]{theTexture, theIndex, null});
        }
        
        public static AbstractShaderNode AbstractShaderNodePassThrough(AbstractShaderNode theValue)
        {
            var getBaseType = typeof(PassThroughNode<>);
            var memberInfo = theValue.GetType().BaseType;
            if (memberInfo == null) return null;
            var dataType = new[] { memberInfo.GetGenericArguments()[0] };
            var getType = getBaseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, theValue) as AbstractShaderNode;
        }
        
        public static AbstractShaderNode AbstractDeclareValue(AbstractShaderNode theValue)
        {
            return CreateAbstract(theValue, typeof(DeclareValue<>), new object[]{null});
        }
        
        public static AbstractShaderNode AbstractConstant(AbstractShaderNode theGpuValue, float theValue)
        {
            var dataType = new[] { theGpuValue.GetType().BaseType.GetGenericArguments()[0]};
            return ConstantHelper.AbstractFromFloat(dataType[0], theValue);
        }
        
        public static AbstractShaderNode AbstractGetMember(ShaderNode<GpuStruct> theStruct, AbstractShaderNode theMember)
        {
            var getMemberBaseType = typeof(GetMember<,>);
            var dataType = new Type[] {typeof(GpuStruct), theMember.GetType().BaseType.GetGenericArguments()[0]};
            var getMemberType = getMemberBaseType.MakeGenericType(dataType);
            var getMemberInstance = Activator.CreateInstance(getMemberType, theStruct, theMember.Name, null) as AbstractShaderNode;
            return getMemberInstance;
        }
        
        public static AbstractShaderNode AbstractDeclareValueAssigned(AbstractShaderNode theMember)
        {
            return CreateAbstract(theMember, typeof(DeclareValue<>), new object[] {theMember});
        }
        
        public static AbstractShaderNode AbstractAssignNode(AbstractShaderNode theTarget, AbstractShaderNode theSource)
        {
            return CreateAbstract(theTarget, typeof(AssignNode<>), new object[] {theTarget, theSource});
        }
    }
    
    public static class ShaderNodesUtil
    {

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

        public static string Evaluate(string theShaderTemplate, IDictionary<string,string> theKeys)
        {
            return Regex.Replace(
                theShaderTemplate, 
                @"\$\{(?<key>[^}]+)\}", 
                m => theKeys.ContainsKey(m.Groups["key"].Value) ? theKeys[m.Groups["key"].Value] : m.Value
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

        public static void AddShaderSource(Game game, string type, string sourceCode, string sourcePath)
        {
            var effectSystem = game.EffectSystem;
            var compiler = effectSystem.Compiler as EffectCompiler;
            if (compiler is null && effectSystem.Compiler is EffectCompilerCache effectCompilerCache)
                compiler = typeof(EffectCompilerChain).GetProperty("Compiler", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(effectCompilerCache) as EffectCompiler;
            
            if (compiler == null) return;
            
            var getParserMethod = typeof(EffectCompiler).GetMethod("GetMixinParser", BindingFlags.Instance | BindingFlags.NonPublic);
            if (getParserMethod == null) return;
            if (!(getParserMethod.Invoke(compiler, null) is ShaderMixinParser parser)) return;
            
            var sourceManager = parser.SourceManager;
            sourceManager.AddShaderSource(type, sourceCode, sourcePath);
        }

        // ReSharper disable once UnusedMember.Global
        // accessed from vl
        public static VLComputeEffectShader RegisterComputeShader<T>(Game game, ToComputeFx<T> theComputeFx)
        {
            var shaderGraph = ShaderGraph.BuildFinalShaderGraph(theComputeFx);
            var computeShader = ShaderGraph.ComposeComputeShader(game.GraphicsDevice, game.Services, shaderGraph);
            AddShaderSource(game, theComputeFx.ShaderName, theComputeFx.ShaderCode, "shaders\\" + theComputeFx.ShaderName + ".sdsl");
            
            return computeShader;
        }

        // ReSharper disable once UnusedMember.Global
        // accessed from vl
        public static ToShaderFX<T> RegisterShaderFX<T>(Game game, ShaderNode<T> theGpuValue, bool isCompute = true) where T : struct
        {
            var toShaderFx = new ToShaderFX<T>(game,theGpuValue);
            AddShaderSource(game, toShaderFx.ShaderName, toShaderFx.ShaderCode, "shaders\\" + toShaderFx.ShaderName + ".sdsl");
            return toShaderFx;
        }
        
        public static ToComputeMatrix RegisterComputeMatrix(Game game, ShaderNode<Matrix> theGpuValue) 
        {
            var toComputeMatrix = new ToComputeMatrix(game,theGpuValue);
            AddShaderSource(game, toComputeMatrix.ShaderName, toComputeMatrix.ShaderCode, "shaders\\" + toComputeMatrix.ShaderName + ".sdsl");
            return toComputeMatrix;
        }

        public static void ListInputs(List<IGpuInput> theInputs, AbstractShaderNode theGpuValue)
        {
            if (theGpuValue == null) return;
            
            theInputs.AddRange(theGpuValue.InputList());
        }
        
        public static Dictionary<string, List<TResource>> ResourcesForTreeBreadthFirst<TResource>(AbstractShaderNode theNode)
        {
            var result = new Dictionary<string, List<TResource>>();
            Trees.ReadOnlyTreeNode.TraverseBreadthFirst(theNode.Tree, node =>
            {
                if (!(node is ShaderTree tree)) return Trees.SkipAllChilds;
                tree.Node.Resources.ForEach(kv =>
                {
                    var values = kv.Value.OfType<TResource>();
                    var tResources = values.ToList();
                    if (tResources.IsEmpty()) return;
                    if (!result.ContainsKey(kv.Key))
                    {
                        result[kv.Key] = new List<TResource>();
                    }
                    tResources.ForEach(v => result[kv.Key].Add(v));
                });
                return Trees.TraverseAllChilds;
            }, out _);
            return result;
        }
        
        
        
        public static List<TResource> ResourcesForTreeBreadthFirstList<TResource>(AbstractShaderNode theNode)
        {
            var result = new List<TResource>();
            Trees.ReadOnlyTreeNode.TraverseBreadthFirst(theNode.Tree, node =>
            {
                if (!(node is ShaderTree tree)) return Trees.SkipAllChilds;
                tree.Node.Resources.ForEach(kv =>
                {
                    var values = kv.Value.OfType<TResource>();
                    var tResources = values as TResource[] ?? values.ToArray();
                    if (tResources.IsEmpty()) return;
                    tResources.ForEach(v => result.Add(v));
                });
                return Trees.TraverseAllChilds;
            }, out _);
            return result;
        }
        
        public static Dictionary<string, List<TResource>> ResourcesForTree<TResource>(AbstractShaderNode theNode)
        {
            var result = new Dictionary<string, List<TResource>>();
            Trees.ReadOnlyTreeNode.Traverse(theNode.Tree, node =>
            {
                if (!(node is ShaderTree tree)) return Trees.SkipAllChilds;
                tree.Node.Resources.ForEach(kv =>
                {
                    var values = kv.Value.OfType<TResource>();
                    var tResources = values as TResource[] ?? values.ToArray();
                    if (tResources.IsEmpty()) return;
                    if (!result.ContainsKey(kv.Key))
                    {
                        result[kv.Key] = new List<TResource>();
                    }
                    tResources.ForEach(v => result[kv.Key].Add(v));
                });
                return Trees.TraverseAllChilds;
            }, out _, out _);
            return result;
        }
        
        
        
        public static List<TResource> ResourcesForTreeList<TResource>(AbstractShaderNode theNode)
        {
            var result = new List<TResource>();
            Trees.ReadOnlyTreeNode.Traverse(theNode.Tree, node =>
            {
                if (!(node is ShaderTree tree)) return Trees.SkipAllChilds;
                tree.Node.Resources.ForEach(kv =>
                {
                    var values = kv.Value.OfType<TResource>();
                    var tResources = values as TResource[] ?? values.ToArray();
                    if (tResources.IsEmpty()) return;
                    tResources.ForEach(v => result.Add(v));
                });
                return Trees.TraverseAllChilds;
            }, out _, out _);
            return result;
        }
        
        static readonly PropertyKey<int> VarIDCounterKey = new PropertyKey<int>("Fuse.FuseIDCounter", typeof(int), DefaultValueMetadata.Static(0, keepDefaultValue: true));

        
        internal static int GetAndIncIDCount(this ShaderGeneratorContext context)
        {
            var result = context.Tags.Get(VarIDCounterKey);
            context.Tags.Set(VarIDCounterKey, result + 1);
            return result;
        }
    }
}
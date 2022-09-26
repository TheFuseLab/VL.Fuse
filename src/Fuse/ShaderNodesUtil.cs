using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Fuse.ShaderFX;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering.Materials;
using Stride.Shaders.Compiler;
using Stride.Shaders.Parser;
using VL.Core;
using VL.Lib.Collections;
using VL.Stride;
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

    public static class AbstractCreation
    {
        public static AbstractShaderNode CreateAbstract(AbstractShaderNode theValue, Type theBaseType, object[] theArguments )
        {
            var nodeType = theValue.GetType();

            while (nodeType != null && nodeType.BaseType != null && nodeType.BaseType != typeof(AbstractShaderNode))
            {
                nodeType = nodeType.BaseType;
            }
            
            var dataType = new[] { nodeType.GetGenericArguments()[0]};
            var getType = theBaseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, theArguments) as AbstractShaderNode;
        }
        
        public static AbstractShaderNode AbstractGetMember<T>(NodeContext nodeContext, int theContextId, ShaderNode<T> theStruct, AbstractShaderNode theMember)
        {
           // return CreateAbstract(theMember, typeof(GetMember<,>), new object[]{theStruct, theMember.Name, null});
            
            
            var getMemberBaseType = typeof(GetMember<,>);
            var nodeType = theMember.GetType();
            while (nodeType != null && nodeType.BaseType != null && nodeType.BaseType != typeof(AbstractShaderNode))
            {
                nodeType = nodeType.BaseType;
            }
            var dataType = new[] {typeof(T), nodeType.GetGenericArguments()[0]};
            var getType = getMemberBaseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, ShaderNodesUtil.GetContext(nodeContext, theContextId), theStruct, theMember.Name, null) as AbstractShaderNode;
            
        }
        
        public static AbstractShaderNode AbstractComputeTextureGet(NodeContext nodeContext, int theContextId, ShaderNode<Texture> theTexture, AbstractShaderNode theIndex, AbstractShaderNode theValue)
        {
            return CreateAbstract(theValue, typeof(PassThroughNode<>), new object[]{ShaderNodesUtil.GetContext(nodeContext, theContextId), theTexture, theIndex, null});
        }
        
        public static AbstractShaderNode AbstractShaderNodePassThrough(NodeContext nodeContext, int theContextId, AbstractShaderNode theValue)
        {
            return CreateAbstract(theValue, typeof(PassThroughNode<>), new object[]{ShaderNodesUtil.GetContext(nodeContext, theContextId), "pass", theValue});
            /*
            var getBaseType = typeof(PassThroughNode<>);
            var nodeType = theValue.GetType().BaseType;
            if (nodeType == null) return null;
            var dataType = new[] { nodeType.GetGenericArguments()[0] };
            var getType = getBaseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, theValue) as AbstractShaderNode;
            */
        }
        
        public static AbstractShaderNode AbstractDeclareValue(NodeContext nodeContext, int theContextId, AbstractShaderNode theValue)
        {
            return CreateAbstract(theValue, typeof(DeclareValue<>), new object[]{ShaderNodesUtil.GetContext(nodeContext, theContextId)});
        }
        
        public static AbstractShaderNode AbstractDeclareValueAssigned(NodeContext nodeContext, int theContextId, AbstractShaderNode theValue)
        {
            return CreateAbstract(theValue, typeof(DeclareValue<>), new object[] {ShaderNodesUtil.GetContext(nodeContext, theContextId), theValue});
        }

        public static void CallFunction(AbstractShaderNode theNode, string theFunctionName, object[] theArgs)
        {
            var resultType = theNode.GetType();
            var method = resultType.GetMethod(theFunctionName);
            method?.Invoke(theNode, theArgs);
        }
        
        public static AbstractShaderNode AbstractOutput(NodeContext nodeContext, int theContextId, AbstractShaderNode theComputation, AbstractShaderNode theOutput)
        {
            var result = CreateAbstract(theOutput, typeof(Output<>), new object[] {ShaderNodesUtil.GetContext(nodeContext, theContextId), null});
            CallFunction(result,"SetInputsAbstract",new object[]{ theComputation, theOutput});
            return result;
        }
        
        public static AbstractShaderNode AbstractAssignNode(NodeContext nodeContext, int theContextId, AbstractShaderNode theTarget, AbstractShaderNode theSource)
        {
            return CreateAbstract(theTarget, typeof(AssignValue<>), new object[] {ShaderNodesUtil.GetContext(nodeContext, theContextId), theTarget, theSource});
        }
        
        public static AbstractShaderNode AbstractConstant(NodeContext nodeContext, int theContextId, AbstractShaderNode theGpuValue, float theValue)
        {
            var dataType = new[] { theGpuValue.GetType().BaseType.GetGenericArguments()[0]};
            return ConstantHelper.AbstractFromFloat(ShaderNodesUtil.GetContext(nodeContext, theContextId), dataType[0], theValue);
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
            var computeShader = ShaderGraph.ComposeComputeShader(game.GraphicsDevice, game.Services, shaderGraph);
            AddShaderSource( theComputeFx.ShaderName, theComputeFx.ShaderCode, "shaders\\" + theComputeFx.ShaderName + ".sdsl");
            
            return computeShader;
        }

        // ReSharper disable once UnusedMember.Global
        // accessed from vl
        public static ToShaderFX<T> RegisterShaderFX<T>(ShaderNode<T> theGpuValue, bool isCompute = true) where T : struct
        {
            var toShaderFx = new ToShaderFX<T>(theGpuValue);
            AddShaderSource( toShaderFx.ShaderName, toShaderFx.ShaderCode, "shaders\\" + toShaderFx.ShaderName + ".sdsl");
            return toShaderFx;
        }
        
        public static ToComputeMatrix RegisterComputeMatrix(ShaderNode<Matrix> theGpuValue) 
        {
            var game = ServiceRegistry.Current.GetGameProvider().GetHandle().Resource;
            if (game == null) return null;
            
            var toComputeMatrix = new ToComputeMatrix(game,theGpuValue);
            AddShaderSource( toComputeMatrix.ShaderName, toComputeMatrix.ShaderCode, "shaders\\" + toComputeMatrix.ShaderName + ".sdsl");
            return toComputeMatrix;
        }

        public static void ListInputs(List<IGpuInput> theInputs, AbstractShaderNode theGpuValue)
        {
            if (theGpuValue == null) return;
            
            theInputs.AddRange(theGpuValue.InputList());
        }
        
        public static Dictionary<string, List<TProperty>> PropertiesForTreeBreadthFirst<TProperty>(AbstractShaderNode theNode)
        {
            var result = new Dictionary<string, List<TProperty>>();
            Trees.ReadOnlyTreeNode.TraverseBreadthFirst(theNode.Tree, node =>
            {
                if (!(node is ShaderTree tree)) return Trees.SkipAllChilds;
                tree.Node.Property.ForEach(kv =>
                {
                    var values = kv.Value.OfType<TProperty>();
                    var tProperties = values.ToList();
                    if (tProperties.IsEmpty()) return;
                    if (!result.ContainsKey(kv.Key))
                    {
                        result[kv.Key] = new List<TProperty>();
                    }
                    tProperties.ForEach(v => result[kv.Key].Add(v));
                });
                return Trees.TraverseAllChilds;
            }, out _);
            return result;
        }
        
        
        
        public static List<TProperty> PropertiesForTreeBreadthFirstList<TProperty>(AbstractShaderNode theNode)
        {
            var result = new List<TProperty>();
            Trees.ReadOnlyTreeNode.TraverseBreadthFirst(theNode.Tree, node =>
            {
                if (!(node is ShaderTree tree)) return Trees.SkipAllChilds;
                tree.Node.Property.ForEach(kv =>
                {
                    var values = kv.Value.OfType<TProperty>();
                    var tProperties = values as TProperty[] ?? values.ToArray();
                    if (tProperties.IsEmpty()) return;
                    tProperties.ForEach(v => result.Add(v));
                });
                return Trees.TraverseAllChilds;
            }, out _);
            return result;
        }
        
        public static Dictionary<string, List<TProperty>> PropertiesForTree<TProperty>(AbstractShaderNode theNode)
        {
            var result = new Dictionary<string, List<TProperty>>();
            Trees.ReadOnlyTreeNode.Traverse(theNode.Tree, node =>
            {
                if (!(node is ShaderTree tree)) return Trees.SkipAllChilds;
                tree.Node.Property.ForEach(kv =>
                {
                    var values = kv.Value.OfType<TProperty>();
                    var tProperties = values as TProperty[] ?? values.ToArray();
                    if (tProperties.IsEmpty()) return;
                    if (!result.ContainsKey(kv.Key))
                    {
                        result[kv.Key] = new List<TProperty>();
                    }
                    tProperties.ForEach(v => result[kv.Key].Add(v));
                });
                return Trees.TraverseAllChilds;
            }, out _, out _);
            return result;
        }
        
        
        
        public static List<TProperty> PropertiesForTreeList<TProperty>(AbstractShaderNode theNode)
        {
            var result = new List<TProperty>();
            Trees.ReadOnlyTreeNode.Traverse(theNode.Tree, node =>
            {
                if (!(node is ShaderTree tree)) return Trees.SkipAllChilds;
                tree.Node.Property.ForEach(kv =>
                {
                    var values = kv.Value.OfType<TProperty>();
                    var tProperties = values as TProperty[] ?? values.ToArray();
                    if (tProperties.IsEmpty()) return;
                    tProperties.ForEach(v => result.Add(v));
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

        public static int Id2=0;

        public static int GetAndIncIDCount3()
        {
            return Id2++;
        }
        
        public static uint GetHashCode(NodeContext nodeContext)
        {
            var myHashCode = new HashCode();

            if (nodeContext == null) return (uint) myHashCode.ToHashCode();
            
            foreach (var p in nodeContext.Path.Stack)
            {
                myHashCode.Add(p.ElementId);
            }
            unchecked{
                return (uint)myHashCode.ToHashCode();
            }
        }

        public static NodeContext GetContext(NodeContext nodeContext, int theID)
        {
            return nodeContext?.CreateSubContext(new UniqueId(nodeContext.AppContext.DocumentId, theID.ToString()));
        }

        public static void ReplaceNode(AbstractShaderNode theOrigin, AbstractShaderNode theReplacement)
        {
            var myConnectedNodes = new List<AbstractShaderNode>(theOrigin.Outs);

            foreach (var myConnectedNode in myConnectedNodes)
            {
                for (var i = 0; i < myConnectedNode.Ins.Count; i++)
                {
                    if (myConnectedNode.Ins[i] != theOrigin) continue;
                    
                    theOrigin.Outs.Remove(myConnectedNode);
                    myConnectedNode.Ins[i] = theReplacement;
                    theReplacement.Outs.Add(myConnectedNode);
                }
            }
        }
    }
}
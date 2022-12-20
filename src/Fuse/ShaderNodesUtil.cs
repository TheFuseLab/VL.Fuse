using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
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

        private static Type GetBaseType(AbstractShaderNode theNode)
        {
            var nodeType = theNode.GetType();
            while (nodeType is {BaseType: { }} && nodeType.BaseType != typeof(AbstractShaderNode))
            {
                nodeType = nodeType.BaseType;
            }

            return nodeType;
        }
        
        public static AbstractShaderNode AbstractGetMember<T>(NodeSubContextFactory theSubContextFactory, ShaderNode<T> theStruct, AbstractShaderNode theMember)
        {
           //return CreateAbstract(theMember, typeof(GetMember<,>), new object[]{theSubContextFactory.NextSubContext(), theStruct, theMember.Name, null});
            
           
            var getMemberBaseType = typeof(GetMember<,>);
            var nodeType = GetBaseType(theMember);
            var dataType = new[] {typeof(T), nodeType.GetGenericArguments()[0]};
            var getType = getMemberBaseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), theStruct, theMember.Name, null) as AbstractShaderNode;
          
        }
        
        public static AbstractShaderNode AbstractSetMember<T>(NodeSubContextFactory theSubContextFactory, ShaderNode<T> theStruct, string theMember, AbstractShaderNode theValue)
        {
            //return CreateAbstract(theMember, typeof(AssignValueToMember<>), new object[]{theSubContextFactory.NextSubContext(), theStruct, theMember.Name, theValue});
            
            
            var setMemberBaseType = typeof(AssignValueToMember<>);
            var dataType = new[] {typeof(T)};
            var getType = setMemberBaseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), theStruct, theMember, theValue) as AbstractShaderNode;
           
        }
        
        public static AbstractShaderNode AbstractComputeTextureGet(NodeSubContextFactory theSubContextFactory, ShaderNode<Texture> theTexture, AbstractShaderNode theIndex, AbstractShaderNode theValue)
        {
            
            var indexType = GetBaseType(theIndex);
            var valueType = GetBaseType(theValue);
            
            var baseType = typeof(ComputeTextureGet<,>);
            var dataType = new[] {indexType.GetGenericArguments()[0], valueType.GetGenericArguments()[0]};
            var getType = baseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), theTexture, theIndex, null) as AbstractShaderNode;
        }
        
        public static AbstractShaderNode AbstractComputeTextureSet(NodeSubContextFactory theSubContextFactory, ShaderNode<Texture> theTexture, AbstractShaderNode theIndex, AbstractShaderNode theValue)
        {
            
            var indexType = GetBaseType(theIndex);
            var valueType = GetBaseType(theValue);
            
            var baseType = typeof(ComputeTextureSet<,>);
            var dataType = new[] {indexType.GetGenericArguments()[0], valueType.GetGenericArguments()[0]};
            var getType = baseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, theSubContextFactory.NextSubContext(), theTexture, theIndex, theValue) as AbstractShaderNode;
        }
        
        public static AbstractShaderNode AbstractShaderNodePassThrough(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theValue)
        {
            return CreateAbstract(theValue, typeof(PassThroughNode<>), new object[]{theSubContextFactory.NextSubContext(), "pass", theValue});
            /*
            var getBaseType = typeof(PassThroughNode<>);
            var nodeType = theValue.GetType().BaseType;
            if (nodeType == null) return null;
            var dataType = new[] { nodeType.GetGenericArguments()[0] };
            var getType = getBaseType.MakeGenericType(dataType);
            return Activator.CreateInstance(getType, theValue) as AbstractShaderNode;
            */
        }
        
        public static AbstractShaderNode AbstractDeclareValue(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theValue)
        {
            return CreateAbstract(theValue, typeof(DeclareValue<>), new object[]{theSubContextFactory.NextSubContext()});
        }
        
        public static AbstractShaderNode AbstractDeclareValueAssigned(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theValue)
        {
            return CreateAbstract(theValue, typeof(DeclareValue<>), new object[] {theSubContextFactory.NextSubContext(), theValue});
        }

        public static void CallFunction(AbstractShaderNode theNode, string theFunctionName, object[] theArgs)
        {
            var resultType = theNode.GetType();
            var method = resultType.GetMethod(theFunctionName);
            method?.Invoke(theNode, theArgs);
        }
        
        public static AbstractShaderNode AbstractOutput(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theComputation, AbstractShaderNode theOutput)
        {
            var result = CreateAbstract(theOutput, typeof(Output<>), new object[] {theSubContextFactory.NextSubContext(), null});
            CallFunction(result,"SetInputsAbstract",new object[]{ theComputation, theOutput});
            return result;
        }
        
        public static AbstractShaderNode AbstractAssignNode(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theTarget, AbstractShaderNode theSource)
        {
            return CreateAbstract(theTarget, typeof(AssignValue<>), new object[] {theSubContextFactory.NextSubContext(), theTarget, theSource});
        }
        
        public static AbstractShaderNode AbstractConstant(NodeSubContextFactory theSubContextFactory, AbstractShaderNode theGpuValue, float theValue)
        {
            var dataType = new[] { theGpuValue.GetType().BaseType.GetGenericArguments()[0]};
            return ConstantHelper.AbstractFromFloat(theSubContextFactory.NextSubContext(), dataType[0], theValue);
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
            var game = ServiceRegistry.Current.GetGameProvider().GetHandle().Resource;
            if (game == null) return null;
            
            var effectImageShader = new DynamicDrawEffectInstance("ShaderFXGraphEffect");
            var context = typeof(ShaderGraph).GetMethod("NewShaderGeneratorContext",BindingFlags.Static)?.Invoke(null, new object[]{game.GraphicsDevice, effectImageShader.Parameters, effectImageShader.Subscriptions});
          //  var context = ShaderGraph.NewShaderGeneratorContext(game.GraphicsDevice, effectImageShader.Parameters, effectImageShader.Subscriptions);
            var key = new MaterialComputeColorKeys(MaterialKeys.DiffuseMap, MaterialKeys.DiffuseValue, Color.White);
            theDrawShader.GenerateShaderSource((ShaderGeneratorContext) context,key);
            AddShaderSource( theDrawShader.ShaderName, theDrawShader.ShaderCode, "shaders\\" + theDrawShader.ShaderName + ".sdsl");
            effectImageShader.EffectName = theDrawShader.ShaderName;
            return effectImageShader;
        }

        // ReSharper disable once UnusedMember.Global
        // accessed from vl
        public static ToShaderFX<T> RegisterShaderFX<T>(ShaderNode<T> theGpuValue, bool isCompute = true) where T : struct
        {
            var toShaderFx = new ToShaderFX<T>(theGpuValue);
            AddShaderSource( toShaderFx.ShaderName, toShaderFx.ShaderCode, "shaders\\" + toShaderFx.ShaderName + ".sdsl");
            return toShaderFx;
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
                if (node is not ShaderTree tree) return Trees.SkipAllChilds;
                tree.Node.Property.ForEach(kv =>
                {
                    var (key, value) = kv;
                    var values = value.OfType<TProperty>();
                    var tProperties = values as TProperty[] ?? values.ToArray();
                    if (tProperties.IsEmpty()) return;
                    if (!result.ContainsKey(key))
                    {
                        result[key] = new List<TProperty>();
                    }
                    tProperties.ForEach(v => result[key].Add(v));
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

    public class NodeSubContextFactory
    {
        
        private int _subContextId = 0;

        private int _startIndex = 0;

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
            if(_context == null)return NodeContext.Default;
            
            var result = _context.CreateSubContext(new UniqueId("Fuse", _subContextId.ToString()));
            _subContextId++;
            return result;
        }
    }
}
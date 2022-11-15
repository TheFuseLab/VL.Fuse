using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core.Extensions;
using Stride.Graphics;
using VL.Core;
using VL.Stride.Shaders.ShaderFX;

namespace Fuse.compute
{
    
    public abstract class AbstractTypedFunction<TIn,TOut>: ShaderNode<TOut> where TIn : struct
    {
        

        protected AbstractTypedFunction(NodeContext nodeContext, string theName, ShaderNode<TOut> theDefault = null) : base(nodeContext,  theName, theDefault)
        {
            AddProperty(Structs, BuildStructs());
        }

        private static string BuildStructs()
        {
            if (!TypeHelpers.IsStructType<TIn>()) return "";
            
            const string shaderCode = 
                @"    struct ${structName}{
${structMembers}
    };" ;
            
            var call = new StringBuilder();
            typeof(TIn).GetProperties().ForEach(property =>
            {
                call.Append("        "+TypeHelpers.GetGpuType(property.PropertyType) + " " + property.Name+";"+Environment.NewLine);
            });
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"structName", typeof(TIn).Name},
                {"structMembers", call.ToString()}
            });
        }
    }
    
    public class DeclareStructVariable<T>: AbstractTypedFunction<T,T> where T : struct
    {
        
        public DeclareStructVariable(NodeContext nodeContext, ConstantValue<T> theDefault) : base( nodeContext, "getBuffer")
        {
            SetInputs(new List<AbstractShaderNode>(){});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${resultType} ${resultName};";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>());
        }
    }
    
    public class TypedBufferGet<T> : AbstractTypedFunction<T,T> where T : struct
    {
        private ShaderNode<Buffer<T>> _buffer;
        private ShaderNode<int> _index;
        
        public TypedBufferGet(NodeContext nodeContext, ShaderNode<T> theDefault) : base( nodeContext, "getBuffer", theDefault)
        {
            
        }

        public void SetInputs(ShaderNode<Buffer<T>> theBuffer, ShaderNode<int> theIndex)
        {
            _buffer = theBuffer;
            _index = theIndex;
            SetInputs(new List<AbstractShaderNode>(){theBuffer,theIndex});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${resultType} ${resultName} = ${bufferName}[${index}];";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"bufferName", _buffer.ID},
                {"index", _index.ID}
            });
        }
    }
    
    public class TypedBufferSet<TIn> : AbstractTypedFunction<TIn, GpuVoid>, IComputeVoid where TIn : struct
    {
        private ShaderNode<Buffer<TIn>> _buffer;
        private ShaderNode<int> _index;
        private ShaderNode<TIn> _value;
    
        public TypedBufferSet(NodeContext nodeContext) : base( nodeContext, "setBuffer")
        {
            
        }

        public void SetInputs(ShaderNode<Buffer<TIn>> theBuffer, ShaderNode<int> theIndex, ShaderNode<TIn> theValue)
        {
            _buffer = theBuffer;
            _index = theIndex;
            _value = theValue;
            
            SetInputs(new List<AbstractShaderNode>(){theBuffer,theIndex,theValue});
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${bufferName}[${index}] = ${value};";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"bufferName", _buffer.ID},
                {"index", _index.ID},
                {"value", _value.ID}
            });
        }
    }
    
    public class TypedBufferAppend<T> : AbstractTypedFunction<T, GpuVoid> where T : struct
    {
        private ShaderNode<Buffer<T>> _buffer;
        private ShaderNode<T> _value;
    
        public TypedBufferAppend(NodeContext nodeContext) : base( nodeContext, "appendBuffer")
        {
            
        }

        public void SetInputs(ShaderNode<Buffer<T>> theBuffer, ShaderNode<T> theValue)
        {
            _buffer = theBuffer;
            _value = theValue;
            
            SetInputs(new List<AbstractShaderNode>(){theBuffer,theValue});
        }
        
        protected override Dictionary<string, string> CreateTemplateMap()
        {
            return new Dictionary<string, string>();
        }
        
        protected override string GenerateDefaultSource()
        {
            return "";
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${bufferName}.Append(${value});";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"bufferName", _buffer.ID},
                {"value", _value.ID}
            });
        }
    }
    
    public class TypedBufferConsume<T> : AbstractTypedFunction<T,T> where T : struct
    {
        private ShaderNode<Buffer<T>> _buffer;
        
        public TypedBufferConsume(NodeContext nodeContext, ShaderNode<T> theDefault) : base( nodeContext, "consumeBuffer", theDefault)
        {
            
        }

        public void SetInputs(ShaderNode<Buffer<T>> theBuffer)
        {
            _buffer = theBuffer;
            SetInputs(new List<AbstractShaderNode>{theBuffer});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${resultType} ${resultName} = ${bufferName}.Consume();";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"bufferName", _buffer.ID}
            });
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text;
using Stride.Core.Extensions;
using Stride.Graphics;

namespace Fuse.compute
{
    
    public abstract class AbstractTypedFunction<TIn,TOut>: ShaderNode<TOut> where TIn : struct
    {
        

        protected AbstractTypedFunction(string theName, ConstantValue<TOut> theDefault = null) : base( theName, theDefault)
        {
            AddResource(Structs, BuildStructs());
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
                call.Append("        "+TypeHelpers.GetGpuTypeForType(property.PropertyType) + " " + property.Name+";"+Environment.NewLine);
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
        
        public DeclareStructVariable(ConstantValue<T> theDefault) : base( "getBuffer")
        {
            Setup(new List<AbstractGpuValue>(){});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${resultType} ${resultName};";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>());
        }
    }
    
    public class TypedBufferGet<T> : AbstractTypedFunction<T,T> where T : struct
    {
        private readonly GpuValue<Buffer<T>> _buffer;
        private readonly GpuValue<int> _index;
        
        public TypedBufferGet(GpuValue<Buffer<T>> theBuffer, GpuValue<int> theIndex, ConstantValue<T> theDefault) : base( "getBuffer", theDefault)
        {
            _buffer = theBuffer;
            _index = theIndex;
            Setup(new List<AbstractGpuValue>(){theBuffer,theIndex});
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
    
    public class TypedBufferSet<TIn> : AbstractTypedFunction<TIn, GpuVoid> where TIn : struct
    {
        private readonly GpuValue<Buffer<TIn>> _buffer;
        private readonly GpuValue<int> _index;
        private readonly GpuValue<TIn> _value;
    
        public TypedBufferSet(GpuValue<Buffer<TIn>> theBuffer, GpuValue<int> theIndex, GpuValue<TIn> theValue) : base( "setBuffer")
        {
            _buffer = theBuffer;
            _index = theIndex;
            _value = theValue;
            
            Setup(new List<AbstractGpuValue>(){theBuffer,theIndex,theValue});
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
        private readonly GpuValue<Buffer<T>> _buffer;
        private readonly GpuValue<T> _value;
    
        public TypedBufferAppend(GpuValue<Buffer<T>> theBuffer, GpuValue<T> theValue) : base( "appendBuffer")
        {
            _buffer = theBuffer;
            _value = theValue;
            
            Setup(new List<AbstractGpuValue>(){theBuffer,theValue});
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
        private readonly GpuValue<Buffer<T>> _buffer;
        
        public TypedBufferConsume(GpuValue<Buffer<T>> theBuffer, ConstantValue<T> theDefault) : base( "consumeBuffer", theDefault)
        {
            _buffer = theBuffer;
            Setup(new List<AbstractGpuValue>(){theBuffer});
        }

        protected override string SourceTemplate()
        {
            const string shaderCode = "${bufferName}.Consume();";
            return ShaderNodesUtil.Evaluate(shaderCode,new Dictionary<string, string>()
            {
                {"bufferName", _buffer.ID}
            });
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Fuse;
using Fuse.function;
using VL.Core;

namespace Fuse.function
{
    public class Invoke<T> : ShaderNode<T>
    {
        private readonly Delegate<T> _delegate;
        public Invoke(NodeContext nodeContext, Delegate<T> theDelegate, IEnumerable<AbstractShaderNode> theValues, string theId = "Invoke", ShaderNode<T> theDefault = null) : base(nodeContext, theId, theDefault)
        {
            Functions = new Dictionary<string, string>();
            
            Name = theId;
            
            _delegate = theDelegate;
            
            OptionalOutputs = new List<AbstractShaderNode>();
         
            SetInputs(theDelegate, theValues);
        }

        private void SetInputs(Delegate<T> theDelegate, IEnumerable<AbstractShaderNode> theValues)
        {
            if (theDelegate == null) return;
            
            
            var inputs = new List<AbstractShaderNode> { theDelegate };
            
            var subContextFactory = new NodeSubContextFactory(NodeContext);
            var myValues = theValues.ToList();
            var countIns = 0;

            foreach (var myParameter in theDelegate.Parameters)
            {
                var myModifier = myParameter.Modifier;
                AbstractShaderNode myValue;
                switch (myModifier)
                {
                    case InputModifier.In:
                        myValue = myValues[countIns++];
                        inputs.Add(myValue);
                        break;
                    case InputModifier.Out:
                        var myDeclareValue = AbstractCreation.AbstractDeclareValue(subContextFactory, myParameter as AbstractShaderNode);
                        inputs.Add(myDeclareValue);
                        var output = AbstractCreation.AbstractOutput(subContextFactory, this, myDeclareValue);
                        OptionalOutputs.Add(output);
                        break;
                    case InputModifier.InOut:
                        myValue = myValues[countIns++];
                        var myDeclareValueAssigned = AbstractCreation.AbstractDeclareValueAssigned(subContextFactory,myValue);
                        inputs.Add(myDeclareValueAssigned);
                        var outputAssigned = AbstractCreation.AbstractOutput(subContextFactory, this, myDeclareValueAssigned);
                        OptionalOutputs.Add(outputAssigned);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            SetInputs(inputs);
        }

        protected override string SourceTemplate()
        {
            if (_delegate == null)
            {
                return GenerateDefaultSource();
            }
            const string shader = "${resultType} ${resultName} = ${functionName}(${arguments});";

            return ShaderNodesUtil.Evaluate(shader, 
                new Dictionary<string, string>
                {
                    {"functionName", _delegate.FunctionName},
                    {"arguments", ShaderNodesUtil.BuildArguments(Ins.GetRange(1, Ins.Count - 1))}
                });
        }

        public sealed override IDictionary<string, string> Functions { get; protected set; }

    }
    
    public class Invoke1In1Out<TIn, TOut> : Invoke<TOut>
    {
        public Invoke1In1Out(NodeContext nodeContext, Delegate1In1Out<TIn, TOut> theDelegate, ShaderNode<TIn> theArg, string theId = "Invoke", ShaderNode<TOut> theDefault = null) : base(nodeContext, theDelegate, new List<AbstractShaderNode>(){theArg}, theId, theDefault)
        {
        }
    }
    
    public class Invoke0In1Out<TOut> : Invoke<TOut>
    {
        public Invoke0In1Out(NodeContext nodeContext, Delegate0In1Out<TOut> theDelegate, string theId = "Invoke", ShaderNode<TOut> theDefault = null) : base(nodeContext, theDelegate, new List<AbstractShaderNode>(), theId, theDefault)
        {
        }
    }

    public class Invoke2In1Out<TIn0, TIn1, TOut> : Invoke<TOut>
    {
        public Invoke2In1Out(NodeContext nodeContext, Delegate2In1Out<TIn0, TIn1, TOut> theDelegate, ShaderNode<TIn0> theArg0, ShaderNode<TIn1> theArg1, string theId = "Invoke", ShaderNode<TOut> theDefault = null) : base(nodeContext, theDelegate, new List<AbstractShaderNode>{theArg0, theArg1}, theId, theDefault)
        {
        }
    }
    
    

    public class Invoke3In1Out<TIn0, TIn1, TIn2, TOut> : Invoke<TOut>
    {
        public Invoke3In1Out(NodeContext nodeContext, Delegate3In1Out<TIn0, TIn1, TIn2, TOut> theDelegate, ShaderNode<TIn0> theArg0, ShaderNode<TIn1> theArg1, ShaderNode<TIn2> theArg2, string theId = "Invoke", ShaderNode<TOut> theDefault = null) : base(nodeContext, theDelegate, new List<AbstractShaderNode>{theArg0, theArg1, theArg2}, theId, theDefault)
        {
        }
    }
}


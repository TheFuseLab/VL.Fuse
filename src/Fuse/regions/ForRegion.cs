using System;
using System.Collections.Generic;
using Fuse.compute;
using System.Linq;
using System.Reactive.Disposables;
using System.Text;
using VL.Core;

namespace Fuse.regions
{
    public class ForGroup : Group
    {
        private readonly ShaderNode<int> _inStart;
        private readonly ShaderNode<int> _inEnd;

        private readonly ForRegion _parentRegion;

        public ForGroup(
            NodeContext nodeContext,
            ForRegion parentRegion,
            ShaderNode<int> inStart,
            ShaderNode<int> inEnd,
            IEnumerable<AbstractShaderNode> theInputs) : base(nodeContext, "ForGroup")
        {
            Name = "ForGroup";
            _parentRegion = parentRegion;
            _inStart = inStart;
            _inEnd = inEnd;

            SetInputs(theInputs);
        }

        protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<uint> theHashes)
        {
            if (!theHashes.Add(HashCode)) return;

            const string shaderCode = @"
        for(int ${index} = ${start}; ${index} < ${end};${index}++){";
            theSourceBuilder.Append(
                ShaderNodesUtil.Evaluate(
                    shaderCode,
                    new Dictionary<string, string>
                    {
                        {"start", _inStart.ID},
                        {"end", _inEnd.ID},
                        {"index",_parentRegion.IndexName}
                    }
                )
            );
            theSourceBuilder.AppendLine();
            var myChildSourceBuilder = new StringBuilder();
            BuildChildrenSource(myChildSourceBuilder, theHashes);
            var myChildSource = myChildSourceBuilder.ToString();
            myChildSource = ShaderNodesUtil.IndentCode(myChildSource);
            theSourceBuilder.Append(myChildSource);

            var source = SourceCode;
            if (!string.IsNullOrWhiteSpace(source.Trim()) && theHashes.Add(HashCode))
            {
                theSourceBuilder.Append("        " + source + Environment.NewLine);
            }

            theSourceBuilder.Append(@"
        }");
            theSourceBuilder.AppendLine();
        }
    }
    
    public class IndexNode : ShaderNode<int>
    {
        
        [ThreadStatic] // Not really needed in your case, but good practice
        private static ForRegion _current; // Could be your loop for example

        public static ForRegion Current => _current;

        public static IDisposable MakeCurrent(ForRegion context)
        {
            var previous = _current;
            _current = context;
            return Disposable.Create(() => 
            { 
                _current = previous; 
            });
        }
        
        public IndexNode(NodeContext nodeContext) : base(nodeContext, "index")
        {
            Name = Current.IndexName;
            SetInputs(new List<AbstractShaderNode>() );
        }
        
        public override string ID => Name;

        protected override string SourceTemplate()
        {
            return "";
        }
    }

    public class ForRegion : ShaderNode<GpuVoid>
    {
        public string IndexName { get; }

        public ForRegion(NodeContext nodeContext) : base(nodeContext, "forRegion")
        {
            OptionalOutputs = new List<AbstractShaderNode>();

            IndexName = "index_" + HashCode;
        }

        public void Setup(
            ShaderNode<int> inStart,
            ShaderNode<int> inEnd,
            IEnumerable<AbstractShaderNode> theInputs,
            IEnumerable<AbstractShaderNode> theOutputs,
            IEnumerable<AbstractShaderNode> theCrossLinks)
        {
            var subContextFactory = new NodeSubContextFactory(NodeContext);
            OptionalOutputs.Clear();

            var inputs = theInputs.ToList();
            var outputs = theOutputs.ToList();
            var crossLinks = theCrossLinks.ToList();

            var myCrossLinks = new List<AbstractShaderNode>();
            crossLinks.ForEach(c =>
            {
                if (c == null) return;
                
                // cross link is a function and handled by connected invoke
                //if (c.Delegates().Count > 0) return;
                
                myCrossLinks.Add(c);
            });

            var myInputs = new List<AbstractShaderNode>();
            var myOutputs = new List<AbstractShaderNode>();
            

            for (var i = 0; i < outputs.Count; i++)
            {
                switch (outputs[i])
                {
                    case ShaderNode<GpuVoid> shaderNode:
                        var myInputVoid = inputs[i];
                        myInputs.Add(myInputVoid);
                        myOutputs.Add(shaderNode);

                        break;
                    default:
                        var myInput = i >= inputs.Count || inputs[i] == null 
                            ? AbstractCreation.AbstractConstant(subContextFactory, outputs[i], 0f) 
                            : inputs[i];
                        var myDeclareValue = AbstractCreation.AbstractDeclareValueAssigned(subContextFactory, myInput);
                        myInputs.Add(myDeclareValue);

                        var myOutput = i >= outputs.Count
                            ? AbstractCreation.AbstractConstant(subContextFactory, inputs[i], 0f)
                            : outputs[i];
                        var myAssign = AbstractCreation.AbstractAssignNode(subContextFactory, myDeclareValue, myOutput);
                        myOutputs.Add(myAssign);

                        break;
                }
            }

            var inCall = new Group(subContextFactory.NextSubContext());
            inCall.SetInput(myInputs);

            var crossLinkCall = new Group(subContextFactory.NextSubContext());
            crossLinkCall.SetInput(myCrossLinks);
            
            for (var i = 0; i < outputs.Count; i++)
            {
                switch (outputs[i])
                {
                    case ShaderNode<GpuVoid> shaderNode:
                        var myInputVoid = myInputs[i] ?? new EmptyVoid(subContextFactory.NextSubContext());
                        var outputVoid = AbstractCreation.AbstractOutput(subContextFactory, this, myInputVoid);
                        OptionalOutputs.Add(outputVoid);

                        break;
                    default:
                        var myInput = myInputs[i] ?? AbstractCreation.AbstractConstant(subContextFactory, outputs[i],0);
                        var output = AbstractCreation.AbstractOutput(subContextFactory, this, myInput);
                        OptionalOutputs.Add(output);

                        break;
                }
            }

            var forGroup = new ForGroup(subContextFactory.NextSubContext(), this, inStart, inEnd, myOutputs);

            var inputList = new List<AbstractShaderNode>
            {
                crossLinkCall,
                inStart,
                inEnd,
                inCall,
                forGroup
            };
            SetInputs(inputList);
        }

        protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<uint> theHashes)
        {
            var nodes = new List<AbstractShaderNode>();
            Children.ForEach(child =>
            {
                if (!(child is ShaderTree input))
                {
                    return;
                }

                nodes.Add(input.Node);
                input.Node.BuildSource(theSourceBuilder, theHashes);
            });


            var source = SourceCode;
            if (!string.IsNullOrWhiteSpace(source) && theHashes.Add(HashCode))
            {
                theSourceBuilder.Append("        " + source + Environment.NewLine);
            }
        }
    }
}
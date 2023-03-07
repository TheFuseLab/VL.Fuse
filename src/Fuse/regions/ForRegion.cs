using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;
using VL.Core;
using VL.Core.PublicAPI;

namespace Fuse.regions
{
    public class ForGroup : Group
    {
        private readonly ShaderNode<int> _inStart;
        private readonly ShaderNode<int> _inEnd;

        private readonly ForRegion _parentRegion;

        private readonly bool _loop;
        private readonly bool _unroll;
        private readonly int _unrollLoops;

        public ForGroup(
            NodeContext nodeContext,
            ForRegion parentRegion,
            ShaderNode<int> inStart,
            ShaderNode<int> inEnd,
            bool theLoop,
            bool theUnroll,
            int theUnrollLoops,
            IEnumerable<AbstractShaderNode> theInputs) : base(nodeContext, "ForGroup")
        {
            Name = "ForGroup";
            _parentRegion = parentRegion;
            _inStart = inStart;
            _inEnd = inEnd;

            _loop = theLoop;
            _unroll = theUnroll;
            _unrollLoops = theUnrollLoops;

            SetInputs(theInputs);
        }

        private string BuildAttributes()
        {
            if (_unrollLoops > 0)
            {
                return "[unroll(" + _unrollLoops + ")]";
            }
            
            if (_unroll)
            {
                return "[unroll]";
            }

            if (_loop)
            {
                return "[loop]"; 
            }

            return "";
        }

        protected internal override void BuildSource(StringBuilder theSourceBuilder, HashSet<string> theHashes, string thePrepend)
        {
            if (!theHashes.Add(ID)) return;
            
            if (ShaderNodesUtil.DebugShaderGeneration) Console.WriteLine(thePrepend + ID);

            const string shaderCode = @"
        ${attributes}
        for(int ${index} = ${start}; ${index} < ${end};${index}++){";
            theSourceBuilder.Append(
                ShaderNodesUtil.Evaluate(
                    shaderCode,
                    new Dictionary<string, string>
                    {
                        { "start", _inStart.ID },
                        { "end", _inEnd.ID },
                        { "index", _parentRegion.IndexName },
                        { "attributes", BuildAttributes() }
                    }
                )
            );
            theSourceBuilder.AppendLine();
            var myChildSourceBuilder = new StringBuilder();
            BuildChildrenSource(myChildSourceBuilder, theHashes, thePrepend);
            var myChildSource = myChildSourceBuilder.ToString();
            myChildSource = ShaderNodesUtil.IndentCode(myChildSource);
            theSourceBuilder.Append(myChildSource);

            var source = SourceCode;
            if (!string.IsNullOrWhiteSpace(source.Trim()) && theHashes.Add(ID))
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
        [field: ThreadStatic] public static ForRegion Current { get; private set; }

        public static IDisposable MakeCurrent(ForRegion context)
        {
            var previous = Current;
            Current = context;
            return Disposable.Create(() => { Current = previous; });
        }

        public IndexNode(NodeContext nodeContext) : base(nodeContext, "index")
        {
            Name = Current.IndexName;
            SetInputs(new List<AbstractShaderNode>());
        }

        public override string ID => Name;

        protected override string SourceTemplate()
        {
            return "";
        }
    }

    public class ForRegion : AbstractRegion
    {
        public string IndexName { get; }

        public ForRegion(NodeContext nodeContext) : base(nodeContext, "forRegion")
        {
            IndexName = "index_" + HashCode;
        }

        public void Setup(
            ShaderNode<int> inStart,
            ShaderNode<int> inEnd,
            bool theLoop,
            bool theUnroll,
            int theUnrollLoops,
            IEnumerable<AbstractShaderNode> theInputs,
            IEnumerable<AbstractShaderNode> theOutputs,
            IEnumerable<AbstractShaderNode> theCrossLinks,
            IEnumerable<BorderControlPointDescription> theDescriptions)
        {
            SetupRegion(
                (subContextFactory, myOutputs) => new ForGroup(
                    subContextFactory.NextSubContext(), 
                    this, 
                    inStart, 
                    inEnd, 
                    theLoop, 
                    theUnroll, 
                    theUnrollLoops, 
                    myOutputs),
                (theInputList) => { theInputList.Add(inStart);theInputList.Add(inEnd);},
                theInputs,
                theOutputs, 
                theCrossLinks, 
                theDescriptions);
        }
    }
}
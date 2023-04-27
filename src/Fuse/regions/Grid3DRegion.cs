using System;
using System.Collections.Generic;
using System.Reactive.Disposables;
using System.Text;
using Fuse.compute;
using Stride.Core.Mathematics;
using VL.Core;
using VL.Core.PublicAPI;

namespace Fuse.regions
{
    public class Grid3DGroup : Group
    {
        private readonly ShaderNode<Int3> _cell;
        private readonly ShaderNode<Int3> _gridDim;

        private readonly BufferInput<Int2> _gridIndices;
        private readonly BufferInput<Int2> _elementIndices;

        private readonly Grid3DRegion _parentRegion;

        public Grid3DGroup(
            NodeContext nodeContext,
            Grid3DRegion parentRegion,
            ShaderNode<Int3> cell,
            ShaderNode<Int3> gridDim,
            BufferInput<Int2> gridIndices,
            BufferInput<Int2> elementIndices,
            IEnumerable<AbstractShaderNode> theInputs) : base(nodeContext, theInputs, "Grid3DGroup")
        {
            Name = "Grid3DGroup";
            _parentRegion = parentRegion;
            _cell = cell;
            _gridDim = gridDim;

            _gridIndices = gridIndices;
            _elementIndices = elementIndices;
        }

        protected override void BuildSource(StringBuilder theSourceBuilder, HashSet<AbstractShaderNode> theHashes, string thePrepend)
        {
            if (!theHashes.Add(this)) return;
            
            if (ShaderNodesUtil.DebugShaderGeneration) Console.WriteLine(thePrepend + ID);

            const string shaderCode = @"
        for (int Z = max(${cell}.z - 1, 0); Z <= min(${cell}.z + 1, ${dim}.z - 1); Z++) 
	    for (int Y = max(${cell}.y - 1, 0); Y <= min(${cell}.y + 1, ${dim}.y - 1); Y++) 
	    for (int X = max(${cell}.x - 1, 0); X <= min(${cell}.x + 1, ${dim}.x - 1); X++){
            
            const unsigned int g_cell = X + Y * ${dim}.x + Z * ${dim}.x * ${dim}.y;	// Calculate Neighbor (or own) Cell ID
            uint2 g_start_end = ${gridIndices}[g_cell];
            
            for (unsigned int n_id = g_start_end.x; n_id < g_start_end.y; n_id++) {
				int ${index} = ${elementIndices}[n_id].y;";
            theSourceBuilder.Append(
                ShaderNodesUtil.Evaluate(
                    shaderCode,
                    new Dictionary<string, string>
                    {
                        { "cell", _cell.ID },
                        { "dim", _gridDim.ID },
                        { "gridIndices", _gridIndices.ID },
                        { "elementIndices", _elementIndices.ID },
                        { "index", _parentRegion.IndexName }
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
            if (!string.IsNullOrWhiteSpace(source.Trim()) && theHashes.Add(this))
            {
                theSourceBuilder.Append("        " + source + Environment.NewLine);
            }

            theSourceBuilder.Append(@"
            }
        }");
            theSourceBuilder.AppendLine();
        }
    }

    public class Index3DNode : ShaderNode<int>
    {
        [field: ThreadStatic] public static NodeContext Current { get; private set; }

        public static IDisposable MakeCurrent(NodeContext context)
        {
            var previous = Current;
            Current = context;
            return Disposable.Create(() => { Current = previous; });
        }

        public Index3DNode(NodeContext nodeContext) : base(nodeContext, "index")
        {
            ID = Current == null ?  "0": $"index_{ShaderNodesUtil.GetHashCode(Current)}";
            SetInputs(new List<AbstractShaderNode>());
        }

        public override string ID { get; }

        protected override string SourceTemplate()
        {
            return "";
        }
    }

    public class Grid3DRegion : AbstractRegion
    {
        public string IndexName { get; }

        public Grid3DRegion(
            NodeContext nodeContext,
            ShaderNode<Int3> cell,
            ShaderNode<Int3> gridDim,
            BufferInput<Int2> gridIndices,
            BufferInput<Int2> elementIndices,
            IEnumerable<AbstractShaderNode> theInputs,
            IEnumerable<AbstractShaderNode> theOutputs,
            IEnumerable<AbstractShaderNode> theCrossLinks,
            IEnumerable<BorderControlPointDescription> theDescriptions) : base(nodeContext, "forRegion")
        {
            IndexName = "index_" + HashCode;
            SetupRegion(
                (subContextFactory, myOutputs) => new Grid3DGroup(
                    subContextFactory.NextSubContext(), 
                    this, 
                    cell, 
                    gridDim, 
                    gridIndices, 
                    elementIndices,  
                    myOutputs),
                (theInputList) => { theInputList.Add(cell);theInputList.Add(gridDim);theInputList.Add(gridIndices);theInputList.Add(elementIndices);},
                theInputs,
                theOutputs, 
                theCrossLinks, 
                theDescriptions);
        }
    }
}
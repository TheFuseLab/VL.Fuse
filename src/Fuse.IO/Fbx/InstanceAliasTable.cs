namespace Fuse.IO.Fbx;

/// <summary>Area-weighted selection among visible scaffold instances. Table slots map back to original
/// instance indices. Empty visibility means nothing visible, not all instances. Uniform scales only.</summary>
[ProcessNode]
public sealed class InstanceAliasTable
{
    private MeshLibrary? _library;
    private int[] _assets=[],_visible=[],_mapping=[],_alias=[];
    private float[] _scales=[],_prob=[];
    private double[] _weights=[];
    private bool _initialized;

    public void Update(MeshLibrary? library, IEnumerable<int> assetIds, IEnumerable<float> scales,
        IEnumerable<int> visibleIndices, out float[] prob, out int[] alias,
        out int[] instanceIndices, out double[] weights, out int count, out bool valid)
    {
        var assets=assetIds?.ToArray()??[];var scale=scales?.ToArray()??[];var visible=visibleIndices?.ToArray()??[];
        if(scale.Length!=0&&scale.Length!=assets.Length)throw new ArgumentException("Supply one actual uniform mesh scale per scaffold instance, or no scales for all ones.",nameof(scales));
        if(scale.Any(s=>!float.IsFinite(s)||s<0))throw new ArgumentException("Scales must be finite and nonnegative.",nameof(scales));
        if(visible.Any(i=>(uint)i>=(uint)assets.Length))throw new ArgumentException("Visible indices must address the full scaffold instance list.",nameof(visibleIndices));
        if(visible.Distinct().Count()!=visible.Length)throw new ArgumentException("Visible indices must be unique; duplicates would bias selection.",nameof(visibleIndices));
        if(!_initialized||!ReferenceEquals(library,_library)||!assets.SequenceEqual(_assets)||!scale.SequenceEqual(_scales)||!visible.SequenceEqual(_visible))
        {
            var mapping=new List<int>();var values=new List<double>();double max=0;
            if(library is not null)foreach(int instance in visible) {
                int id=assets[instance];if((uint)id>=(uint)library.Assets.Length)continue;
                var asset=library.Assets[id];if(asset.Valid==0)continue;
                if(!float.IsFinite(asset.SurfaceArea)||asset.SurfaceArea<0)throw new ArgumentException("Invalid library surface area.",nameof(library));
                double s=scale.Length==0?1:scale[instance];double w=asset.SurfaceArea*s*s;
                if(w<=0)continue;
                mapping.Add(instance);values.Add(w);max=Math.Max(max,w);
            }
            var table=max>0?GpuPacking.BuildTriangleAliasTable(values.Select(w=>(float)(w/max)).ToArray()):new TriangleAliasTable();
            _prob=table.Prob;_alias=table.Alias;_mapping=mapping.ToArray();_weights=values.ToArray();
            _library=library;_assets=assets;_scales=scale;_visible=visible;_initialized=true;
        }
        prob=_prob;alias=_alias;instanceIndices=_mapping;weights=_weights;count=_prob.Length;valid=count>0;
    }
}

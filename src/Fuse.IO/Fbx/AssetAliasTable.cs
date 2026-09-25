namespace Fuse.IO.Fbx;

/// <summary>Weighted selection of library assets. Weight = source surface area * uniform scale squared.
/// Selected table index is AssetId. Empty scales means one for every asset; otherwise supply one per asset.</summary>
[ProcessNode]
public sealed class AssetAliasTable
{
    private MeshLibrary? _library;
    private float[] _scales=[];
    private float[] _prob=[];
    private int[] _alias=[];
    private double[] _weights=[];
    private bool _initialized,_valid;
    public void Update(MeshLibrary? library, IEnumerable<float> scales,
        out float[] prob, out int[] alias, out double[] weights, out int count, out bool valid)
    {
        var input=scales?.ToArray()??[];
        int n=library?.Assets.Length??0;
        if(input.Length!=0&&input.Length!=n)throw new ArgumentException("Supply no scales (all 1), or one uniform scale per library asset.",nameof(scales));
        if(input.Any(s=>!float.IsFinite(s)||s<0))throw new ArgumentException("Scales must be finite and nonnegative.",nameof(scales));
        if(!_initialized||!ReferenceEquals(library,_library)||!input.SequenceEqual(_scales))
        {
            var w=new double[n];double max=0;
            for(int i=0;i<n;i++) {
                var asset=library!.Assets[i];if(asset.Valid==0)continue;
                if(!float.IsFinite(asset.SurfaceArea)||asset.SurfaceArea<0)throw new ArgumentException("Invalid asset surface area.",nameof(library));
                double scale=input.Length==0?1:input[i];
                w[i]=asset.SurfaceArea*scale*scale;max=Math.Max(max,w[i]);
            }
            // Normalize before converting to float to avoid overflow with large scales.
            var table=max>0?GpuPacking.BuildTriangleAliasTable(w.Select(x=>(float)(x/max)).ToArray()):new TriangleAliasTable();
            _prob=table.Prob;_alias=table.Alias;_weights=w;_valid=max>0;
            _library=library;_scales=input;_initialized=true;
        }
        prob=_prob;alias=_alias;weights=_weights;count=_prob.Length;valid=_valid;
    }
}

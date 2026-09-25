namespace Fuse.IO.Fbx;

/// <summary>Shared alias arrays and GPU-ready int4 ranges. X=alias offset, Y=triangle count,
/// Z=global triangle offset, W=valid (0/1). Aliases contain LOCAL triangle indices.</summary>
public sealed class LibraryTriangleAliasTable
{
    public float[] Prob { get; init; } = [];
    public int[] Alias { get; init; } = [];
    public Int4[] AssetRanges { get; init; } = [];
    public Int4[] InstanceRanges { get; init; } = [];
    public int TriangleCount => Prob.Length;
}

/// <summary>Expose existing library tables without rebuilding or duplicating triangles.
/// Instance asset IDs must be in the same order as instance transforms. Invalid IDs retain an empty slot.</summary>
[ProcessNode]
public sealed class TriangleAliasTableForLibrary
{
    private MeshLibrary? _library;
    private int[] _ids=[];
    private LibraryTriangleAliasTable? _table;

    public void Update(MeshLibrary? library, IEnumerable<int> instanceAssetIds,
        out LibraryTriangleAliasTable table, out int triangleCount,
        out float[] prob, out int[] alias, out Int4[] assetRanges, out Int4[] instanceRanges)
    {
        var ids=instanceAssetIds?.ToArray()??[];
        if(_table is null||!ReferenceEquals(library,_library)||!ids.SequenceEqual(_ids))
        {
            _table=BuildTriangleAliasTableForLibrary(library,ids);
            _library=library;_ids=ids;
        }
        table=_table;triangleCount=table.TriangleCount;prob=table.Prob;alias=table.Alias;
        assetRanges=table.AssetRanges;instanceRanges=table.InstanceRanges;
    }

    /// <summary>One buffer containing independent per-asset distributions, NOT one globally normalized distribution.
    /// Empty instance IDs yields no instances; AssetRanges still exposes every library asset.</summary>
    public static LibraryTriangleAliasTable BuildTriangleAliasTableForLibrary(MeshLibrary? library)
        => BuildTriangleAliasTableForLibrary(library, Array.Empty<int>());

    /// <summary>Compatibility overload with optional instance mapping.</summary>
    public static LibraryTriangleAliasTable BuildTriangleAliasTableForLibrary(MeshLibrary? library,IEnumerable<int> instanceAssetIds)
    {
        var ids=instanceAssetIds?.ToArray()??[];
        var assets=library is null?[]:new Int4[library.Assets.Length];
        if(library is not null)
            for(int i=0;i<assets.Length;i++) {
                var a=library.Assets[i];
                if(a.Valid!=0&&a.TriangleCount>0)assets[i]=new(a.AliasOffset,a.TriangleCount,a.TriangleOffset,1);
            }
        var instances=new Int4[ids.Length];
        for(int i=0;i<ids.Length;i++)if((uint)ids[i]<(uint)assets.Length)instances[i]=assets[ids[i]];
        return new(){Prob=library?.AliasProbabilities??[],Alias=library?.AliasIndices??[],AssetRanges=assets,InstanceRanges=instances};
    }
}

/// <summary>Asset-only table node: one library input, shared alias arrays and per-asset ranges.
/// Existing instance-mapping node remains available for patch compatibility.</summary>
[ProcessNode]
public sealed class TriangleAliasTableForAssets
{
    private MeshLibrary? _library;
    private LibraryTriangleAliasTable? _table;
    public void Update(MeshLibrary? library, out int triangleCount,
        out float[] prob, out int[] alias, out Int4[] assetRanges)
    {
        if(_table is null || !ReferenceEquals(library, _library))
        {
            _table=TriangleAliasTableForLibrary.BuildTriangleAliasTableForLibrary(library);
            _library=library;
        }
        triangleCount=_table.TriangleCount;prob=_table.Prob;alias=_table.Alias;assetRanges=_table.AssetRanges;
    }
}

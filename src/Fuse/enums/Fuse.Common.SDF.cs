using System;

namespace Fuse.Common.SDF
{
    // ReSharper disable UnusedType.Global
    // ReSharper disable UnusedMember.Global
    
    public enum CombineSDF
    {
        Union,
        Intersect,
        Difference,
        UnionRound,
        IntersectionRound,
        DifferenceRound,
        UnionChamfer,
        IntersectionChamfer,
        DifferenceChamfer,
        UnionColumns,
        IntersectionColumns,
        DifferenceColumns,
        UnionStairs,
        IntersectionStairs,
        DifferenceStairs,
        Pipe,
        Engrave,
        Groove,
        Tongue
    }

    public enum ExtrudeSDF
    {
        Extrude, 
        ExtrudeChamfer, 
        ExtrudeRound
    }

    public enum GridSDF
    {
        Box, 
        Triangle, 
        Hexagon
    }

    public enum TPMSSDF
    {
        Gyroid, 
        SchwartzP, 
        Diamond, 
        FischerKoch, 
        Lidinoid, 
        Neovius, 
        Octo
    }

    public enum StrutSDF
    {
        CubeCenteredLattice, 
        BodyCenteredLattice, 
        FaceCenteredLattice, 
        CubeAndBodyCenteredLattice, 
        CubeAndFaceCenteredLattice, 
        BodyAndFaceCenteredLattice, 
        CubeAndBodyAndFaceCenteredLattice
    }

    public enum RepeatSDF
    {
        Normal,
        Mirror,
        Positive,
        Interval
    }
}
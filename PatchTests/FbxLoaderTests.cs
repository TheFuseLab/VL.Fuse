using System;
using System.IO;
using System.Linq;
using Fuse.IO.Fbx;
using NUnit.Framework;

namespace PatchTests;

[TestFixture]
public class FbxLoaderTests
{
    private const string StaticMushroom = @"D:\development\imaginary friends\DL_2023_06_DATALAND ASSETS\production\fungi\Fungi Production Tracker\production\type_05_purple_gilled_cluster_v1\models\age_pbr_workflow_v1\assets\type_05_mature_c\exports\type_05_mature_c_pbr.fbx";
    private const string SkinnedModel = @"D:\Users\Christian\3D_Models\Running.fbx";

    private static readonly string[] ExtraAnimations =
    [
        @"D:\Users\Christian\3D_Models\Hip Hop Dancing.fbx",
        @"D:\Users\Christian\3D_Models\Joyful Jump.fbx",
        @"D:\Users\Christian\3D_Models\Rumba Dancing.fbx",
        @"D:\Users\Christian\3D_Models\Silly Dancing.fbx",
        @"D:\Users\Christian\3D_Models\Taunt.fbx"
    ];

    [Test]
    [Explicit("Integration test for the local production FBX asset")]
    public void StaticFbxUsesSyntheticRootWithoutFallingBackToQuad()
    {
        RequireFiles(StaticMushroom);

        var model = FBXLoader.LoadFbx(StaticMushroom);

        Assert.That(IsDefaultFallback(model), Is.False);
        Assert.That(model.Mesh.Vertices.Length, Is.GreaterThan(4));
        Assert.That(model.Mesh.Indices.Length, Is.GreaterThan(6));
        Assert.That(model.BoneNames, Is.EqualTo(new[] { "StaticRoot" }));
        Assert.That(model.Skeleton.Parent, Is.EqualTo(new[] { -1 }));
        Assert.That(model.Anim.ClipNames, Is.EqualTo(new[] { "StaticPose" }));
        Assert.That(model.Anim.Clips, Has.Length.EqualTo(1));
        Assert.That(model.Anim.LocalDeltaDq, Has.Length.EqualTo(1));
        AssertValidMesh(model);

        var minY = model.Mesh.Vertices.Min(vertex => vertex.Position.Y);
        var maxY = model.Mesh.Vertices.Max(vertex => vertex.Position.Y);
        Assert.That(maxY - minY, Is.InRange(0.89f, 0.91f),
            "The FBX node transform must convert Blender Z-up and centimeters to Y-up meters");

        Assert.That(model.Mesh.Vertices.All(vertex =>
            Math.Abs(vertex.Weights.X - 1.0f) < 1e-6f
            && Math.Abs(vertex.Weights.Y) < 1e-6f
            && Math.Abs(vertex.Weights.Z) < 1e-6f
            && Math.Abs(vertex.Weights.W) < 1e-6f), Is.True);
    }

    [Test]
    [Explicit("Integration test for the local Mixamo FBX assets")]
    public void SkinnedFbxAndExtraAnimationsKeepExistingBehavior()
    {
        RequireFiles(new[] { SkinnedModel }.Concat(ExtraAnimations).ToArray());

        var model = FBXLoader.LoadFbxWithExtraAnimations(SkinnedModel, ExtraAnimations);

        Assert.That(IsDefaultFallback(model), Is.False);
        Assert.That(model.BoneNames.Length, Is.GreaterThan(1));
        Assert.That(model.BoneNames, Does.Contain("mixamorig1:Hips"));
        Assert.That(model.Anim.Clips, Has.Length.EqualTo(1 + ExtraAnimations.Length));
        Assert.That(model.Anim.LocalDeltaDq.Length,
            Is.EqualTo(model.Anim.Clips.Sum(clip => clip.FrameCount * clip.BoneCount)));
        AssertValidMesh(model);
        Assert.That(model.Anim.Clips.All(clip => ClipContainsMotion(model, clip)), Is.True);
    }

    private static bool ClipContainsMotion(ModelGpu model, ClipInfoRaster clip)
    {
        for (var bone = 0; bone < clip.BoneCount; bone++)
        {
            var first = model.Anim.LocalDeltaDq[clip.StartIndex + bone];
            for (var frame = 1; frame < clip.FrameCount; frame++)
            {
                var current = model.Anim.LocalDeltaDq[clip.StartIndex + frame * clip.BoneCount + bone];
                if (Math.Abs(first.Qr.X - current.Qr.X) > 1e-5f
                    || Math.Abs(first.Qr.Y - current.Qr.Y) > 1e-5f
                    || Math.Abs(first.Qr.Z - current.Qr.Z) > 1e-5f
                    || Math.Abs(first.Qr.W - current.Qr.W) > 1e-5f
                    || Math.Abs(first.Qd.X - current.Qd.X) > 1e-5f
                    || Math.Abs(first.Qd.Y - current.Qd.Y) > 1e-5f
                    || Math.Abs(first.Qd.Z - current.Qd.Z) > 1e-5f
                    || Math.Abs(first.Qd.W - current.Qd.W) > 1e-5f)
                    return true;
            }
        }

        return false;
    }

    private static void AssertValidMesh(ModelGpu model)
    {
        Assert.That(model.Mesh.Indices.All(index => index >= 0 && index < model.Mesh.Vertices.Length), Is.True);
        Assert.That(model.Mesh.Vertices.All(vertex =>
        {
            var sum = vertex.Weights.X + vertex.Weights.Y + vertex.Weights.Z + vertex.Weights.W;
            return float.IsFinite(sum) && Math.Abs(sum - 1.0f) < 0.001f;
        }), Is.True);
    }

    private static bool IsDefaultFallback(ModelGpu model)
    {
        return model.Mesh.Vertices.Length == 4
               && model.Mesh.Indices.Length == 6
               && model.BoneNames.SequenceEqual(new[] { "DefaultRoot" })
               && model.Anim.ClipNames.SequenceEqual(new[] { "DefaultClip" });
    }

    private static void RequireFiles(params string[] paths)
    {
        var missing = paths.Where(path => !File.Exists(path)).ToArray();
        if (missing.Length > 0)
            Assert.Ignore("Missing local FBX fixtures: " + string.Join(", ", missing));
    }
}

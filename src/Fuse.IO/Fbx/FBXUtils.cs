using System.Diagnostics;
using Assimp;
using Quaternion = Stride.Core.Mathematics.Quaternion;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Fuse.IO.Fbx;

// -------------------- GPU structs --------------------

/// <summary>
///     Vertex layout for GPU (StructuredBuffer); tightly packed / sequential.
/// </summary>
public struct GpuVertex
{
    public Vector3 Position; // 12
    public Vector3 Normal; // 24
    public Vector2 Tex; // 32
    public Int4 Bone; // 48
    public Vector4 Weights; // 64 bytes total
}

public struct SkeletonGpu
{
    public int[] Parent; // bone -> parent (-1 root)
    public Matrix[] InverseBind; // bind^-1 (Assimp Bone.OffsetMatrix)
    public Matrix[] BindLocal; // optional (node.Transform), useful if you build bindLocal on GPU
}

public struct DualQuat
{
    public Vector4 Qr, Qd;
} // (x,y,z,w), (x,y,z,w)

public struct ClipInfoRaster
{
    public int BoneCount;
    public int FrameCount;

    // index into LocalDeltaDQ (DualQuat-units)
    public int StartIndex;

    public float TicksPerSecond;
    public float StartTick;
    public float StepTick;
    public float DurationSeconds;
}

public sealed class AnimBankGpu
{
    public string[] ClipNames = [];
    public ClipInfoRaster[] Clips = [];

    // delta-local DQs: animLocal = restInvLocal * currentLocal
    public DualQuat[] LocalDeltaDq = [];

    // optional convenience if you ever want ready-to-skin on GPU
    public DualQuat[] SkinDq = [];
}

public sealed class MeshGpu
{
    public int BoneCount;
    public int[] Indices = []; // 32-bit for simplicity
    public GpuVertex[] Vertices = [];
}

public sealed class ModelGpu
{
    public AnimBankGpu Anim = new();

    // mapping, only for debugging or tools
    public string[] BoneNames = [];
    public MeshGpu Mesh = new();
    public SkeletonGpu Skeleton;
}

public sealed class TriangleAliasTable
{
    /// <summary>Alias index per triangle.</summary>
    public int[] Alias = [];

    /// <summary>Probability per triangle (Walker alias method, normalized so expected value is 1).</summary>
    public float[] Prob = [];

    /// <summary>Number of triangles this table was built for.</summary>
    public int TriangleCount => Prob.Length;
}

// -------------------- Loader --------------------

public static class FBXLoader
{
    private static SkeletonBuildResult BuildRobustSkeleton(Scene scene)
    {
        var nodeByName = new Dictionary<string, Node>(StringComparer.Ordinal);
        Walk(scene.RootNode, n => nodeByName[n.Name] = n);

        var weightedBoneNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var mesh in scene.Meshes)
        foreach (var bone in mesh.Bones)
            weightedBoneNames.Add(bone.Name);

        if (weightedBoneNames.Count == 0)
            throw new InvalidOperationException("No bones with vertex weights found in the scene.");

        static List<Node> GetPathToRoot(Node node)
        {
            var path = new List<Node>();
            while (node != null)
            {
                path.Add(node);
                node = node.Parent;
            }

            path.Reverse();
            return path;
        }

        var bonePaths = weightedBoneNames
            .Where(name => nodeByName.ContainsKey(name))
            .Select(name => GetPathToRoot(nodeByName[name]))
            .ToList();

        if (bonePaths.Count == 0)
            throw new InvalidOperationException("Could not find any of the weighted bones in the scene graph.");

        var rootPath = new List<Node>(bonePaths[0]);
        foreach (var path in bonePaths.Skip(1))
        {
            var commonDepth = 0;
            for (var i = 0; i < Math.Min(rootPath.Count, path.Count); i++)
            {
                if (rootPath[i] != path[i]) break;
                commonDepth = i + 1;
            }

            rootPath.RemoveRange(commonDepth, rootPath.Count - commonDepth);
        }

        var skeletonRootNode = rootPath.LastOrDefault();
        if (skeletonRootNode == null)
            throw new InvalidOperationException("Failed to find a common skeleton root.");

        var orderedBoneNames = new List<string>();
        Walk(skeletonRootNode, n => orderedBoneNames.Add(n.Name));

        var indexOf = orderedBoneNames.Select((name, i) => (name, i))
            .ToDictionary(t => t.name, t => t.i, StringComparer.Ordinal);

        var parentIndices = new int[orderedBoneNames.Count];
        for (var i = 0; i < orderedBoneNames.Count; i++)
        {
            var name = orderedBoneNames[i];
            var node = nodeByName[name];

            if (node == skeletonRootNode || node.Parent == null ||
                !indexOf.TryGetValue(node.Parent.Name, out var parentIndex))
                parentIndices[i] = -1;
            else
                parentIndices[i] = parentIndex;
        }

        return new SkeletonBuildResult
        {
            OrderedBoneNames = orderedBoneNames,
            IndexOf = indexOf,
            ParentIndices = parentIndices,
            NodeByName = nodeByName
        };
    }

    private static float DetermineSceneScale(Scene scene)
    {
        if (scene.Metadata != null &&
            scene.Metadata.TryGetValue("GlobalScale", out var meta))
        {
            // FBX stores GlobalScale in centimeters
            if (meta.Data is float f)
                return f * 0.01f; // cm → m

            if (meta.Data is double d)
                return (float)d * 0.01f; // cm → m

            // Unexpected type: warn but continue
            Debug.WriteLine(
                $"[FBXLoader] Warning: GlobalScale found but type was {meta.Data.GetType()}.");
        }

        // fallback → assume already in meters
        return 1.0f;
    }


    public static ModelGpu LoadFbx(string path, float sceneScale = 0.01f, float fpsIfNeeded = 30f)
    {
        try
        {
            var ctx = new AssimpContext();
            var pps = PostProcessSteps.Triangulate
                      | PostProcessSteps.JoinIdenticalVertices
                      | PostProcessSteps.LimitBoneWeights
                      | PostProcessSteps.ImproveCacheLocality
                      | PostProcessSteps.OptimizeMeshes
                      | PostProcessSteps.OptimizeGraph;

            var scene = ctx.ImportFile(path, pps);
            if (scene == null || scene.RootNode == null)
                throw new InvalidOperationException("Failed to load scene.");

            var skeletonBuild = BuildRobustSkeleton(scene);
            var orderedBones = skeletonBuild.OrderedBoneNames;
            var indexOf = skeletonBuild.IndexOf;
            var nodeByName = skeletonBuild.NodeByName;
            var nb = orderedBones.Count;

            // Load original, unscaled local transforms for the bind pose
            var bindLocal = new Matrix[nb];
            for (var i = 0; i < nb; i++)
            {
                var name = orderedBones[i];
                bindLocal[i] = nodeByName.TryGetValue(name, out var nn) ? ToM(nn.Transform) : Matrix.Identity;
            }

            // Load original InverseBind matrices from the file
            var invBindByName = new Dictionary<string, Matrix>(StringComparer.Ordinal);
            foreach (var mesh in scene.Meshes)
            foreach (var bone in mesh.Bones)
                invBindByName[bone.Name] = ToM(bone.OffsetMatrix);

            var invBind = new Matrix[nb];
            var scaleMatrix = Matrix.Scaling(sceneScale);
            var inverseScaleMatrix = Matrix.Scaling(1.0f / sceneScale);

            for (var i = 0; i < nb; i++)
            {
                var name = orderedBones[i];
                var originalInvBind = invBindByName.TryGetValue(name, out var m) ? m : Matrix.Identity;

                invBind[i] = inverseScaleMatrix * originalInvBind * scaleMatrix;
            }

            var skeleton = new SkeletonGpu
            {
                Parent = skeletonBuild.ParentIndices,
                InverseBind = invBind,
                BindLocal = bindLocal
            };

            var meshGpu = BuildMesh(scene, indexOf, sceneScale);
            // --- prepare anim bank ---
            var bindPoseDecomposed = new (Vector3 S, Quaternion R, Vector3 T)[bindLocal.Length];
            for (var i = 0; i < bindLocal.Length; i++)
                bindLocal[i].Decompose(out bindPoseDecomposed[i].S,
                    out bindPoseDecomposed[i].R,
                    out bindPoseDecomposed[i].T);

            var finalDqBuffer = new List<DualQuat>();
            var finalClips = new List<ClipInfoRaster>();
            var finalClipNames = new List<string>();

            // animations from this main file
            BuildAnimationsInto(scene, orderedBones, bindPoseDecomposed, fpsIfNeeded, sceneScale, finalDqBuffer,
                finalClips, finalClipNames);

            var anim = new AnimBankGpu
            {
                LocalDeltaDq = finalDqBuffer.ToArray(),
                Clips = finalClips.ToArray(),
                ClipNames = finalClipNames.ToArray()
            };

            return new ModelGpu
            {
                Skeleton = skeleton,
                Mesh = meshGpu,
                Anim = anim,
                BoneNames = orderedBones.ToArray()
            };
        }
        catch (Exception e)
        {
            Debug.WriteLine($"Failed to load model '{path}'. Reason: {e.Message}. Returning default quad.");
            return BuildDefault();
        }
    }

    private static void EnsureCompatibleSkeleton(Scene animScene, List<string> canonicalBones)
    {
        // simplest: ensure all canonical bones exist as nodes in animScene
        var nodes = new HashSet<string>(StringComparer.Ordinal);
        Walk(animScene.RootNode, n => nodes.Add(n.Name));

        foreach (var b in canonicalBones)
            if (!nodes.Contains(b))
                Debug.WriteLine(
                    $"Warning: animation scene missing bone '{b}'. It will use bind pose for that bone.");
    }

    public static ModelGpu LoadFbxWithExtraAnimations(
        string mainPath,
        IEnumerable<string> extraAnimationPaths,
        float sceneScale = 0.01f,
        float fpsIfNeeded = 30f)
    {
        try
        {
            var ctx = new AssimpContext();
            var pps = PostProcessSteps.Triangulate
                      | PostProcessSteps.JoinIdenticalVertices
                      | PostProcessSteps.LimitBoneWeights
                      | PostProcessSteps.ImproveCacheLocality
                      | PostProcessSteps.OptimizeMeshes
                      | PostProcessSteps.OptimizeGraph;

            // ---------- 1) Load main FBX (rig + mesh + base animations) ----------
            var sceneMain = ctx.ImportFile(mainPath, pps);
            if (sceneMain == null || sceneMain.RootNode == null)
                throw new InvalidOperationException($"Failed to load main scene '{mainPath}'.");

            var skeletonBuild = BuildRobustSkeleton(sceneMain);
            var orderedBones = skeletonBuild.OrderedBoneNames;
            var indexOf = skeletonBuild.IndexOf;
            var nodeByName = skeletonBuild.NodeByName;
            var nb = orderedBones.Count;

            // BindLocal (original transforms from main file)
            var bindLocal = new Matrix[nb];
            for (var i = 0; i < nb; i++)
            {
                var name = orderedBones[i];
                bindLocal[i] = nodeByName.TryGetValue(name, out var nn)
                    ? ToM(nn.Transform)
                    : Matrix.Identity;
            }

            // InverseBind (scaled to sceneScale), same as in LoadFbx
            var invBindByName = new Dictionary<string, Matrix>(StringComparer.Ordinal);
            foreach (var mesh in sceneMain.Meshes)
            foreach (var bone in mesh.Bones)
                invBindByName[bone.Name] = ToM(bone.OffsetMatrix);


            var invBind = new Matrix[nb];
            var scaleMatrix = Matrix.Scaling(sceneScale);
            var inverseScaleMatrix = Matrix.Scaling(1.0f / sceneScale);

            for (var i = 0; i < nb; i++)
            {
                var name = orderedBones[i];
                var originalInvBind = invBindByName.TryGetValue(name, out var m)
                    ? m
                    : Matrix.Identity;

                invBind[i] = inverseScaleMatrix * originalInvBind * scaleMatrix;
            }

            var skeleton = new SkeletonGpu
            {
                Parent = skeletonBuild.ParentIndices,
                InverseBind = invBind,
                BindLocal = bindLocal
            };

            // Mesh from main file
            var meshGpu = BuildMesh(sceneMain, indexOf, sceneScale);

            // Decomposed bind pose (shared across all animation FBX)
            var bindPoseDecomposed = new (Vector3 S, Quaternion R, Vector3 T)[bindLocal.Length];
            for (var i = 0; i < bindLocal.Length; i++)
                bindLocal[i].Decompose(out bindPoseDecomposed[i].S,
                    out bindPoseDecomposed[i].R,
                    out bindPoseDecomposed[i].T);

            // ---------- 2) Build combined animation bank ----------
            var finalDqBuffer = new List<DualQuat>();
            var finalClips = new List<ClipInfoRaster>();
            var finalClipNames = new List<string>();

            // 2a) animations from main file
            BuildAnimationsInto(
                sceneMain,
                orderedBones,
                bindPoseDecomposed,
                fpsIfNeeded,
                sceneScale,
                finalDqBuffer,
                finalClips,
                finalClipNames);

            // 2b) animations from extra FBX files
            if (extraAnimationPaths != null)
                foreach (var animPath in extraAnimationPaths)
                {
                    if (string.IsNullOrWhiteSpace(animPath))
                        continue;

                    Scene animScene;
                    try
                    {
                        animScene = ctx.ImportFile(animPath, pps);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(
                            $"[FBXLoader] Failed to load animation scene '{animPath}': {ex.Message}. Skipping.");
                        continue;
                    }

                    if (animScene == null || animScene.RootNode == null)
                    {
                        Debug.WriteLine(
                            $"[FBXLoader] Animation scene '{animPath}' has no root node. Skipping.");
                        continue;
                    }

                    // Optional: sanity check for missing bones (non-fatal)
                    EnsureCompatibleSkeleton(animScene, orderedBones);

                    BuildAnimationsInto(
                        animScene,
                        orderedBones,
                        bindPoseDecomposed,
                        fpsIfNeeded,
                        sceneScale,
                        finalDqBuffer,
                        finalClips,
                        finalClipNames);
                }

            var animBank = new AnimBankGpu
            {
                LocalDeltaDq = finalDqBuffer.ToArray(),
                Clips = finalClips.ToArray(),
                ClipNames = finalClipNames.ToArray()
            };

            // ---------- 3) Final ModelGpu ----------
            return new ModelGpu
            {
                Skeleton = skeleton,
                Mesh = meshGpu,
                Anim = animBank,
                BoneNames = orderedBones.ToArray()
            };
        }
        catch (Exception e)
        {
            Debug.WriteLine(
                $"Failed to load model '{mainPath}' (with extra animations). Reason: {e.Message}. Returning default quad.");
            return BuildDefault();
        }
    }


    private static MeshGpu BuildMesh(Scene scene, Dictionary<string, int> indexOf, float sceneScale)
    {
        var relevantMeshes = scene.Meshes.Where(m => m.HasBones && m.VertexCount > 0).ToList();
        if (relevantMeshes.Count == 0)
            throw new InvalidOperationException("No skinned mesh with vertices found.");

        var totalVertices = relevantMeshes.Sum(m => m.VertexCount);
        var totalIndices = relevantMeshes.Sum(m => m.FaceCount * 3);

        var vertices = new GpuVertex[totalVertices];
        var indices = new int[totalIndices];
        var vertexWeights = new List<(int bone, float w)>[totalVertices];
        for (var i = 0; i < totalVertices; i++) vertexWeights[i] = new List<(int, float)>();

        var vertexOffset = 0;
        var indexOffset = 0;

        foreach (var m in relevantMeshes)
        {
            for (var v = 0; v < m.VertexCount; v++)
            {
                vertices[vertexOffset + v].Position =
                    m.HasVertices ? ToVector3(m.Vertices[v]) * sceneScale : Vector3.Zero;
                vertices[vertexOffset + v].Normal = m.HasNormals ? ToVector3(m.Normals[v]) : Vector3.UnitY;
                if (m.HasTextureCoords(0))
                {
                    var t = m.TextureCoordinateChannels[0][v];
                    vertices[vertexOffset + v].Tex = new Vector2(t.X, t.Y);
                }
            }

            foreach (var b in m.Bones)
            {
                if (!indexOf.TryGetValue(b.Name, out var bi)) continue;
                foreach (var w in b.VertexWeights)
                    if (w.VertexID < m.VertexCount)
                        vertexWeights[vertexOffset + w.VertexID].Add((bi, w.Weight));
            }

            foreach (var f in m.Faces)
                if (f.IndexCount == 3)
                {
                    indices[indexOffset++] = vertexOffset + f.Indices[0];
                    indices[indexOffset++] = vertexOffset + f.Indices[1];
                    indices[indexOffset++] = vertexOffset + f.Indices[2];
                }

            vertexOffset += m.VertexCount;
        }

        for (var v = 0; v < totalVertices; v++)
        {
            var list = vertexWeights[v].OrderByDescending(w => w.w).Take(4).ToList();
            var sum = list.Sum(w => w.w);
            var inv = sum > 1e-8f ? 1f / sum : 0f;

            var boneIds = new Int4(0);
            var ws = Vector4.Zero;
            if (list.Count > 0)
            {
                boneIds.X = list[0].bone;
                ws.X = list[0].w * inv;
            }

            if (list.Count > 1)
            {
                boneIds.Y = list[1].bone;
                ws.Y = list[1].w * inv;
            }

            if (list.Count > 2)
            {
                boneIds.Z = list[2].bone;
                ws.Z = list[2].w * inv;
            }

            if (list.Count > 3)
            {
                boneIds.W = list[3].bone;
                ws.W = list[3].w * inv;
            }

            vertices[v].Bone = boneIds;
            vertices[v].Weights = ws;
        }

        return new MeshGpu
        {
            Vertices = vertices,
            Indices = indices,
            BoneCount = indexOf.Count
        };
    }

    private static void BuildAnimationsInto(
        Scene scene,
        List<string> orderedBones,
        (Vector3 S, Quaternion R, Vector3 T)[] bindPoseDecomposed,
        float fpsIfNeeded,
        float sceneScale,
        List<DualQuat> finalDqBuffer,
        List<ClipInfoRaster> finalClips,
        List<string> finalClipNames)
    {
        var boneIndex = orderedBones
            .Select((n, i) => (n, i))
            .ToDictionary(t => t.n, t => t.i, StringComparer.Ordinal);

        foreach (var anim in scene.Animations)
        {
            var tps = anim.TicksPerSecond > 0 ? anim.TicksPerSecond : 25.0;
            var durTicks = anim.DurationInTicks;

            // channel per bone index (only for bones we know)
            var channelsByBoneId = anim.NodeAnimationChannels
                .Where(ch => boneIndex.ContainsKey(ch.NodeName))
                .ToDictionary(ch => boneIndex[ch.NodeName]);

            var step = (float)(tps / Math.Max(1e-6, fpsIfNeeded)); // ticks per frame
            var frameCount = Math.Max(1, (int)Math.Ceiling(durTicks / step) + 1);

            var clipStartIndex = finalDqBuffer.Count;

            for (var f = 0; f < frameCount; f++)
            {
                double tick = f * step;

                for (var b = 0; b < orderedBones.Count; b++)
                {
                    var (bindS, bindR, bindT) = bindPoseDecomposed[b];
                    var finalS = bindS;
                    var finalR = bindR;
                    var finalT = bindT;

                    if (channelsByBoneId.TryGetValue(b, out var channel))
                    {
                        if (channel.HasPositionKeys)
                        {
                            var i0 = FindPositionKey(channel.PositionKeys, tick);
                            var i1 = Math.Min(i0 + 1, channel.PositionKeys.Count - 1);

                            var k0 = channel.PositionKeys[i0];
                            var k1 = channel.PositionKeys[i1];

                            var timeDiff = k1.Time - k0.Time;
                            var u = timeDiff > 0 ? (float)((tick - k0.Time) / timeDiff) : 0f;

                            var interpolatedPos = Vector3.Lerp(ToVector3(k0.Value), ToVector3(k1.Value), u);
                            finalT = interpolatedPos;
                        }

                        if (channel.HasRotationKeys)
                        {
                            var i0 = FindRotationKey(channel.RotationKeys, tick);
                            var i1 = Math.Min(i0 + 1, channel.RotationKeys.Count - 1);

                            var k0 = channel.RotationKeys[i0];
                            var k1 = channel.RotationKeys[i1];

                            var timeDiff = k1.Time - k0.Time;
                            var u = timeDiff > 0 ? (float)((tick - k0.Time) / timeDiff) : 0f;

                            finalR = Quaternion.Slerp(ToQ(k0.Value), ToQ(k1.Value), u);
                        }
                    }

                    var finalLocalMatrix =
                        Matrix.Scaling(finalS) *
                        Matrix.RotationQuaternion(finalR) *
                        Matrix.Translation(finalT * sceneScale);

                    finalDqBuffer.Add(MatrixRigidToDQ(finalLocalMatrix));
                }
            }

            finalClips.Add(new ClipInfoRaster
            {
                BoneCount = orderedBones.Count,
                FrameCount = frameCount,
                StartIndex = clipStartIndex,
                TicksPerSecond = (float)tps,
                StartTick = 0,
                StepTick = step,
                DurationSeconds = (float)(tps > 0 ? durTicks / tps : 0)
            });
            finalClipNames.Add(anim.Name);
        }
    }


    private static int FindPositionKey(IReadOnlyList<VectorKey> keys, double time)
    {
        if (keys.Count <= 1) return 0;
        int lo = 0, hi = keys.Count - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) >> 1;
            if (keys[mid].Time <= time) lo = mid;
            else hi = mid;
        }

        return lo;
    }

    private static int FindRotationKey(IReadOnlyList<QuaternionKey> keys, double time)
    {
        if (keys.Count <= 1) return 0;
        int lo = 0, hi = keys.Count - 1;
        while (hi - lo > 1)
        {
            var mid = (lo + hi) >> 1;
            if (keys[mid].Time <= time) lo = mid;
            else hi = mid;
        }

        return lo;
    }

    /// <summary>
    ///     Creates a minimal, 1x1 unit-sized, valid ModelGpu object to prevent downstream failures.
    ///     The quad has correct winding for a right-handed system (Y-up, front-face pointing to +Z).
    /// </summary>
    private static ModelGpu BuildDefault()
    {
        // 1. A simple 1x1 quad mesh bound to one bone
        var vertices = new GpuVertex[4];

        // Positions are centered, making a 1x1 quad. Normal points towards positive Z.
        // Tex Coords have (0,0) at the top-left.
        vertices[0] = new GpuVertex
        {
            Position = new Vector3(-0.5f, -0.5f, 0), Normal = Vector3.UnitZ, Tex = new Vector2(0, 1),
            Bone = new Int4(0), Weights = Vector4.UnitX
        }; // Bottom-left
        vertices[1] = new GpuVertex
        {
            Position = new Vector3(0.5f, -0.5f, 0), Normal = Vector3.UnitZ, Tex = new Vector2(1, 1), Bone = new Int4(0),
            Weights = Vector4.UnitX
        }; // Bottom-right
        vertices[2] = new GpuVertex
        {
            Position = new Vector3(0.5f, 0.5f, 0), Normal = Vector3.UnitZ, Tex = new Vector2(1, 0), Bone = new Int4(0),
            Weights = Vector4.UnitX
        }; // Top-right
        vertices[3] = new GpuVertex
        {
            Position = new Vector3(-0.5f, 0.5f, 0), Normal = Vector3.UnitZ, Tex = new Vector2(0, 0), Bone = new Int4(0),
            Weights = Vector4.UnitX
        }; // Top-left

        var mesh = new MeshGpu
        {
            Vertices = vertices,
            // Indices are in Counter-Clockwise (CCW) order for right-handed rendering
            Indices = [0, 1, 2, 0, 2, 3],
            BoneCount = 1
        };

        // 2. A minimal skeleton with a single root bone
        var skeleton = new SkeletonGpu
        {
            Parent = [-1],
            InverseBind = [Matrix.Identity],
            BindLocal = [Matrix.Identity]
        };

        // 3. A minimal animation bank with one clip containing one identity frame
        var anim = new AnimBankGpu
        {
            LocalDeltaDq = [IdentityDQ()],
            Clips =
            [
                new ClipInfoRaster
                {
                    BoneCount = 1,
                    FrameCount = 1,
                    StartIndex = 0,
                    TicksPerSecond = 30,
                    StartTick = 0,
                    StepTick = 1,
                    DurationSeconds = 0
                }
            ],
            ClipNames = ["DefaultClip"]
        };

        return new ModelGpu
        {
            Skeleton = skeleton,
            Mesh = mesh,
            Anim = anim,
            BoneNames = ["DefaultRoot"]
        };
    }

    private static DualQuat IdentityDQ()
    {
        return new DualQuat { Qr = new Vector4(0, 0, 0, 1), Qd = Vector4.Zero };
    }

    private static Vector4 QMul(in Vector4 a, in Vector4 b)
    {
        return new Vector4(a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - (a.X * b.X + a.Y * b.Y + a.Z * b.Z));
    }

    private static DualQuat MatrixRigidToDQ(Matrix m)
    {
        m.Decompose(out _, out Quaternion r, out var t);
        return RigidToDQ(r, t);
    }

    private static DualQuat RigidToDQ(Quaternion r, Vector3 t)
    {
        var qr = new Vector4(r.X, r.Y, r.Z, -r.W); // finding this minus sign did cost about 3 days of debugging
        var tq = new Vector4(t, 0f);
        var qd = 0.5f * QMul(tq, qr);
        return new DualQuat { Qr = qr, Qd = qd };
    }

    private static Vector3 ToVector3(Vector3D v)
    {
        return new Vector3(v.X, v.Y, v.Z);
    }

    private static Quaternion ToQ(Assimp.Quaternion q)
    {
        return new Quaternion(q.X, q.Y, q.Z, q.W);
    }

    private static Matrix ToM(Matrix4x4 m)
    {
        return new Matrix(m.A1, m.B1, m.C1, m.D1,
            m.A2, m.B2, m.C2, m.D2,
            m.A3, m.B3, m.C3, m.D3,
            m.A4, m.B4, m.C4, m.D4);
    }

    private static void Walk(Node n, Action<Node> fn)
    {
        fn(n);
        foreach (var c in n.Children) Walk(c, fn);
    }

    private struct SkeletonBuildResult
    {
        public List<string> OrderedBoneNames;
        public Dictionary<string, int> IndexOf;
        public int[] ParentIndices;
        public Dictionary<string, Node> NodeByName;
    }
}

// -------------------- Packing helpers for GPU buffers --------------------

public static class GpuPacking
{
    /// Pack DualQuats into float4[] layout [ qr0, qd0, qr1, qd1, ... ] for a clip.
    /// Base = clip.StartIndex; Count per frame = 2 * BoneCount float4s.
    public static Vector4[] PackDQ(DualQuat[] dqs)
    {
        var out4 = new Vector4[dqs.Length * 2];
        for (var i = 0; i < dqs.Length; i++)
        {
            out4[2 * i + 0] = dqs[i].Qr;
            out4[2 * i + 1] = dqs[i].Qd;
        }

        return out4;
    }

    /// Convert a frame selection (frame, boneIndex) to the packed float4 index.
    public static int DQIndex(int start, int boneCount, int frame, int bone)
    {
        return (start + frame * boneCount + bone) * 2;
    }

    #region TriangleAliasTable

    private static TriangleAliasTable BuildTriangleAliasTable(float[] weights)
    {
        var n = weights.Length;
        var table = new TriangleAliasTable
        {
            Prob = new float[n],
            Alias = new int[n]
        };

        if (n == 0)
            return table;

        var prob = table.Prob;
        var alias = table.Alias;

        // 1) sum weights
        var sum = 0.0;
        for (var i = 0; i < n; i++)
            sum += weights[i];

        if (sum <= 0.0)
        {
            // fallback: uniform
            for (var i = 0; i < n; i++)
            {
                prob[i] = 1.0f;
                alias[i] = i;
            }

            return table;
        }

        var invSum = 1.0 / sum;

        var small = new Queue<int>();
        var large = new Queue<int>();

        // 2) normalize to expected value 1.0 (prob[i] ~ weights[i]/sum * n)
        for (var i = 0; i < n; i++)
        {
            var p = weights[i] * invSum * n;
            prob[i] = (float)p;

            if (p < 1.0) small.Enqueue(i);
            else large.Enqueue(i);
        }

        // 3) distribute probability mass
        while (small.Count > 0 && large.Count > 0)
        {
            var s = small.Dequeue();
            var l = large.Dequeue();

            alias[s] = l;

            var newP = prob[l] + prob[s] - 1.0;
            prob[l] = (float)newP;

            if (newP < 1.0) small.Enqueue(l);
            else large.Enqueue(l);
        }

        // 4) leftover entries
        while (large.Count > 0)
        {
            var i = large.Dequeue();
            prob[i] = 1.0f;
            alias[i] = i;
        }

        while (small.Count > 0)
        {
            var i = small.Dequeue();
            prob[i] = 1.0f;
            alias[i] = i;
        }

        return table;
    }

    public static TriangleAliasTable BuildTriangleAliasTableForMesh(MeshGpu mesh)
    {
        ArgumentNullException.ThrowIfNull(mesh);

        if (mesh.Vertices == null || mesh.Indices == null)
            throw new InvalidOperationException("MeshGpu must have Vertices and Indices set.");

        var triCount = mesh.Indices.Length / 3;
        if (triCount <= 0)
            return new TriangleAliasTable();

        var triAreas = new float[triCount];

        for (var t = 0; t < triCount; t++)
        {
            var i0 = mesh.Indices[t * 3 + 0];
            var i1 = mesh.Indices[t * 3 + 1];
            var i2 = mesh.Indices[t * 3 + 2];

            var p0 = mesh.Vertices[i0].Position;
            var p1 = mesh.Vertices[i1].Position;
            var p2 = mesh.Vertices[i2].Position;

            var e1 = p1 - p0;
            var e2 = p2 - p0;
            var cross = Vector3.Cross(e1, e2);

            var area = 0.5f * cross.Length(); // degenerate tris -> 0
            triAreas[t] = area;
        }

        return BuildTriangleAliasTable(triAreas);
    }

    #endregion
}
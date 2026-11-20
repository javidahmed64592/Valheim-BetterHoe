using System.Collections.Generic;

using BetterHoe.Terrain;

using UnityEngine;

namespace BetterHoe.History;

/// <summary>
/// The raw terrain data of some vertices and paint texels of one terrain compiler: whether each was changed,
/// its level and smooth deltas, and its paint. Restoring it puts that data back exactly. The compiler is found
/// again by its position, as it may have been unloaded and reloaded in between.
/// </summary>
internal sealed class TerrainSnapshot
{
    private Vector3 _position;
    private int[] _heightIndices;
    private bool[] _heightModified;
    private float[] _levelDelta;
    private float[] _smoothDelta;
    private int[] _paintIndices;
    private bool[] _paintModified;
    private Color[] _paint;

    public static TerrainSnapshot Capture(TerrainComp compiler, IList<int> heightIndices, IList<int> paintIndices)
    {
        TerrainSnapshot snapshot = new()
        {
            _position = compiler.transform.position,
            _heightIndices = [.. heightIndices],
            _paintIndices = [.. paintIndices],
            _heightModified = new bool[heightIndices.Count],
            _levelDelta = new float[heightIndices.Count],
            _smoothDelta = new float[heightIndices.Count]
        };
        for (int i = 0; i < heightIndices.Count; i++)
        {
            int index = heightIndices[i];
            snapshot._heightModified[i] = compiler.m_modifiedHeight[index];
            snapshot._levelDelta[i] = compiler.m_levelDelta[index];
            snapshot._smoothDelta[i] = compiler.m_smoothDelta[index];
        }

        snapshot._paintModified = new bool[paintIndices.Count];
        snapshot._paint = new Color[paintIndices.Count];
        for (int i = 0; i < paintIndices.Count; i++)
        {
            int index = paintIndices[i];
            snapshot._paintModified[i] = compiler.m_modifiedPaint[index];
            snapshot._paint[i] = compiler.m_paintMask[index];
        }

        return snapshot;
    }

    /// <summary>
    /// The compiler this snapshot was taken from, if its terrain is loaded.
    /// </summary>
    public TerrainComp FindCompiler()
    {
        TerrainComp compiler = TerrainComp.FindTerrainCompiler(_position);
        return compiler != null && compiler.m_initialized && compiler.m_nview != null && compiler.m_nview.IsValid()
            ? compiler
            : null;
    }

    /// <summary>
    /// The compiler's current data at the same vertices and texels, to restore later.
    /// </summary>
    public TerrainSnapshot CaptureCurrent(TerrainComp compiler)
    {
        return Capture(compiler, _heightIndices, _paintIndices);
    }

    public void Restore(TerrainComp compiler, Vector3 center, float radius)
    {
        ZPackage package = new();
        package.Write(TerrainRpc.Version);
        package.Write(center);
        package.Write(radius);

        package.Write(_heightIndices.Length);
        for (int i = 0; i < _heightIndices.Length; i++)
        {
            package.Write(_heightIndices[i]);
            package.Write(_heightModified[i]);
            package.Write(_levelDelta[i]);
            package.Write(_smoothDelta[i]);
        }

        package.Write(_paintIndices.Length);
        for (int i = 0; i < _paintIndices.Length; i++)
        {
            package.Write(_paintIndices[i]);
            package.Write(_paintModified[i]);
            package.Write(_paint[i].r);
            package.Write(_paint[i].g);
            package.Write(_paint[i].b);
            package.Write(_paint[i].a);
        }

        _ = TerrainRpc.SendRestore(compiler, package);
    }
}

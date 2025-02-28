using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

namespace MOC
{
    public class MaskedOcclusionCulling : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private MeshFilter[] meshFilters;
        private NativeArray<Tile> _tiles;
        public Tile[] Tiles { get; private set; }

        [ContextMenu("RenderMeshes")]
        public void RenderMeshes()
        {
            meshFilters = FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
            Assert.IsTrue(meshFilters != null && cam);
            InitTiles();
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            foreach (var meshFilter in meshFilters)
            {
                RenderMesh(meshFilter);
            }
            stopwatch.Stop();
            Tiles = _tiles.ToArray();
            _tiles.Dispose();
            Debug.Log($"Cost: {stopwatch.ElapsedMilliseconds}ms!");
        }
        
        private void RenderMesh(MeshFilter meshFilter)
        {
            var mesh = meshFilter.sharedMesh;
            var mvpMatrixRaw = cam.projectionMatrix *
                               cam.worldToCameraMatrix *
                               meshFilter.transform.localToWorldMatrix;
            var mvpMatrix = new float4x4(
                mvpMatrixRaw.GetColumn(0), mvpMatrixRaw.GetColumn(1),
                mvpMatrixRaw.GetColumn(2), mvpMatrixRaw.GetColumn(3));

            var vertices = mesh.vertices;
            var indices = mesh.triangles;
            var idxTri = 0;
            var numTris = indices.Length / 3;
            while (idxTri < numTris)
            {
                var startIdxTri = idxTri;
                GatherTransformClip(vertices, indices, mvpMatrix, ref idxTri,
                    out var vtxX, out var vtxY, out var vtxZ);
                TransformToScreenSpace(ref vtxX, ref vtxY, ref vtxZ, out var iVtxX, out var iVtxY);
                ComputeBoundingBox(iVtxX, iVtxY,
                    out var bbTileMinX, out var bbTileMinY, out var bbTileMaxX, out var bbTileMaxY);
                ComputeDepthPlane(vtxX, vtxY, vtxZ, out var zPixelDx, out var zPixelDy);
                for (var i = 0; i < idxTri - startIdxTri; i++)
                {
                    var v0 = new int2(iVtxX[0][i], iVtxY[0][i]);
                    var v1 = new int2(iVtxX[1][i], iVtxY[1][i]);
                    var v2 = new int2(iVtxX[2][i], iVtxY[2][i]);
                    var bbTileRange = new int4(bbTileMinX[i], bbTileMaxX[i], bbTileMinY[i], bbTileMaxY[i]);
                    RasterizeTriangle(v0, v1, v2, bbTileRange, vtxZ[0][i], zPixelDx[i], zPixelDy[i]);
                }
            }
            Debug.Log($"NumTri: {numTris} DONE!");
        }
        
        private void InitTiles()
        {
            _tiles = new NativeArray<Tile>(Constants.NumRowsTile * Constants.NumColsTile, Allocator.Persistent);
            var defaultTile = new Tile
            {
                bitmask = uint4.zero,
                z0 = float.MaxValue,
                z1 = 0.0f
            };
            for (var i = 0; i < _tiles.Length; i++)
            {
                _tiles[i] = defaultTile;
            }
        }
        
        private static void GatherTransformClip(Vector3[] vertices, int[] indices, in float4x4 mvpMatrix, ref int idxTri,
            out float4x3 vtxX, out float4x3 vtxY, out float4x3 vtxZ)
        {
            Assert.IsTrue(idxTri * 3 < indices.Length);
            GatherVertices(vertices, indices, ref idxTri, out vtxX, out vtxY, out vtxZ);
            TransformToNDCSpace(mvpMatrix, ref vtxX, ref vtxY, ref vtxZ);
        }

        private static void GatherVertices(Vector3[] vertices, int[] indices, ref int idxTri,
            out float4x3 vtxX, out float4x3 vtxY, out float4x3 vtxZ)
        {
            vtxX = new float4x3();
            vtxY = new float4x3();
            vtxZ = new float4x3();
            
            for (var i = 0; i < 4; i++)
            {
                var idx = idxTri * 3;
                if (idx >= indices.Length) return;
                idxTri++;
                
                var idx0 = indices[idx];
                var idx1 = indices[idx + 1];
                var idx2 = indices[idx + 2];
                
                var points = new float3[]
                {
                    vertices[idx0],
                    vertices[idx1],
                    vertices[idx2]
                };

                // TODO: 通过 shuffle 收集数据
                for (var j = 0; j < 3; j++)
                {
                    vtxX[j][i] = points[j].x;
                    vtxY[j][i] = points[j].y;
                    vtxZ[j][i] = points[j].z;
                }
            }
        }

        private static void TransformToNDCSpace(in float4x4 mvpMatrix, 
            ref float4x3 vtxX, ref float4x3 vtxY, ref float4x3 vtxZ)
        {
            for (var i = 0; i < 4; i++)
            {
                for (var j = 0; j < 3; j++)
                {
                    var vertex = new float4(vtxX[j][i], vtxY[j][i], vtxZ[j][i], 1f);
                    var transformedVertex = math.mul(mvpMatrix, vertex);
                    vtxX[j][i] = transformedVertex.x / transformedVertex.w;
                    vtxY[j][i] = transformedVertex.y / transformedVertex.w;
                    vtxZ[j][i] = transformedVertex.z / transformedVertex.w;
                }
            }
            // TODO: Clipping 
        }

        private static void TransformToScreenSpace(
            ref float4x3 vtxX, ref float4x3 vtxY, ref float4x3 vtxZ,
            out int4x3 iVtxX, out int4x3 iVtxY
        )
        {
            iVtxX = new int4x3();
            iVtxY = new int4x3();
            for (var i = 0; i < 3; i++)
            {
                iVtxX[i] = math.int4((vtxX[i] * 0.5f + 0.5f) * Constants.ScreenWidth * Constants.SubPixelPrecision);
                iVtxY[i] = math.int4((vtxY[i] * 0.5f + 0.5f) * Constants.ScreenHeight * Constants.SubPixelPrecision);
                vtxX[i] = math.float4(iVtxX[i] / Constants.SubPixelPrecision);
                vtxY[i] = math.float4(iVtxY[i] / Constants.SubPixelPrecision);
                vtxZ[i] = vtxZ[i] * 0.5f + 0.5f;
            }
        }

        private static void ComputeBoundingBox(
            in int4x3 iVtxX, in int4x3 iVtxY,
            out int4 bbTileMinX, out int4 bbTileMinY, out int4 bbTileMaxX, out int4 bbTileMaxY
        )
        {
            var bbPixelMinX = math.min(math.min(iVtxX[0], iVtxX[1]), iVtxX[2]);
            var bbPixelMaxX = math.max(math.max(iVtxX[0], iVtxX[1]), iVtxX[2]);
            var bbPixelMinY = math.min(math.min(iVtxY[0], iVtxY[1]), iVtxY[2]);
            var bbPixelMaxY = math.max(math.max(iVtxY[0], iVtxY[1]), iVtxY[2]);
            bbTileMinX = math.clamp(bbPixelMinX / Constants.SubPixelPrecision / Constants.TileWidth, 0,
                Constants.NumColsTile - 1);
            bbTileMinY = math.clamp(bbPixelMinY / Constants.SubPixelPrecision / Constants.TileHeight, 0,
                Constants.NumRowsTile - 1);
            bbTileMaxX = math.clamp(bbPixelMaxX / Constants.SubPixelPrecision / Constants.TileWidth, 0,
                Constants.NumColsTile - 1);
            bbTileMaxY = math.clamp(bbPixelMaxY / Constants.SubPixelPrecision / Constants.TileHeight, 0,
                Constants.NumRowsTile - 1);
        }
        
        private void RasterizeTriangle(int2 v0, int2 v1, int2 v2, int4 bbTileRange, float z0, float zPixelDx, float zPixelDy)
        {
            // Debug.Log($"Tri: {bbRange.x}, {bbRange.y}, {bbRange.z}, {bbRange.w}");
            var numTiles = (bbTileRange.y - bbTileRange.x + 1) * (bbTileRange.w - bbTileRange.z + 1);
            var outputTiles = new NativeArray<Tile>(numTiles, Allocator.TempJob);
            var rasterizeTriangleJob = new RasterizeTriangleJob
            {
                TileRange = bbTileRange,
                V0 = v0, V1 = v1, V2 = v2,
                Z0 = z0, ZPixelDx = zPixelDx, ZPixelDy = zPixelDy,
                InputTiles = _tiles,
                OutputTiles = outputTiles,
            };
            var jobHandle = rasterizeTriangleJob.Schedule(numTiles, 64);
            jobHandle.Complete();
            
            var i = 0;
            for (var y = bbTileRange.z; y <= bbTileRange.w; y++)
            {
                for (var x = bbTileRange.x; x <= bbTileRange.y; x++)
                {
                    var tileIdx = y * Constants.NumColsTile + x;
                    _tiles[tileIdx] = outputTiles[i++];
                }
            }
            outputTiles.Dispose();
        }
        
        private static void ComputeDepthPlane(
            in float4x3 vtxX, in float4x3 vtxY, in float4x3 vtxZ,
            out float4 zPixelDx, out float4 zPixelDy
        )
        {
            var x2 = vtxX[2] - vtxX[0];
            var x1 = vtxX[1] - vtxX[0];
            var y1 = vtxY[1] - vtxY[0];
            var y2 = vtxY[2] - vtxY[0];
            var z1 = vtxZ[1] - vtxZ[0];
            var z2 = vtxZ[2] - vtxZ[0];

            // 计算分母 d = 1.0f / (x1*y2 - y1*x2)
            var denominator = (x1 * y2) - (y1 * x2);
            var d = math.select(math.rcp(denominator), 0.0f, denominator == 0.0f); // 安全除法，避免除零

            zPixelDx = (z1 * y2 - y1 * z2) * d;
            zPixelDy = (x1 * z2 - z1 * x2) * d;
        }
    }
}
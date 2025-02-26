using System.Diagnostics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

namespace MOC
{
    public class MaskedOcclusionCulling : MonoBehaviour
    {
        [SerializeField] private Tile[] tiles;
        [SerializeField] private Camera cam;
        [SerializeField] private MeshFilter[] meshFilters;
        public Tile[] Tiles => tiles;

        [ContextMenu("RenderMeshes")]
        public void RenderMeshes()
        {
            Assert.IsTrue(meshFilters != null && cam);
            InitTiles();
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            foreach (var meshFilter in meshFilters)
            {
                RenderMesh(meshFilter);
            }
            stopwatch.Stop();
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
                    var bbRange = new int4(bbTileMinX[i], bbTileMaxX[i], bbTileMinY[i], bbTileMaxY[i]);
                    RasterizeTriangle(v0, v1, v2, bbRange, vtxZ[0][i], zPixelDx[i], zPixelDy[i]);
                }
            }
            Debug.Log($"NumTri: {numTris} DONE!");
        }
        
        private void InitTiles()
        {
            tiles = new Tile[Constants.NumRowsTile * Constants.NumColsTile];
            for (var i = 0; i < tiles.Length; i++)
            {
                tiles[i].bitmask = uint4.zero;
                tiles[i].z0 = float.MaxValue;
                tiles[i].z1 = 0.0f;
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
        
        private static bool IsPointInTriangle(int2 p, int2 v0, int2 v1, int2 v2)
        {
            var ab = v1 - v0;
            var bc = v2 - v1;
            var ca = v0 - v2;

            var ap = p - v0;
            var bp = p - v1;
            var cp = p - v2;

            var abXap = ab.x * ap.y - ab.y * ap.x; // AB × AP
            var bcXbp = bc.x * bp.y - bc.y * bp.x; // BC × BP
            var caXcp = ca.x * cp.y - ca.y * cp.x; // CA × CP

            var isAllNonNegative = (abXap >= 0) && (bcXbp >= 0) && (caXcp >= 0);
            var isAllNonPositive = (abXap <= 0) && (bcXbp <= 0) && (caXcp <= 0);
            return isAllNonNegative || isAllNonPositive;
        }
        
        private void RasterizeTriangle(int2 v0, int2 v1, int2 v2, int4 bbRange, float z0, float zPixelDx, float zPixelDy)
        {
            for (var tileX = bbRange.x; tileX <= bbRange.y; tileX++)
            {
                for (var tileY = bbRange.z; tileY <= bbRange.w; tileY++)
                {
                    RasterizeTile(tileX, tileY, v0, v1, v2, z0, zPixelDx, zPixelDy);
                }
            }
        }

        private void RasterizeTile(int tileX, int tileY, int2 v0, int2 v1, int2 v2, float z0, float zPixelDx, float zPixelDy)
        {
            var startX = tileX * Constants.TileWidth;
            var startY = tileY * Constants.TileHeight;
            var tileIdx = tileY * Constants.NumColsTile + tileX;
            var bitmask = uint4.zero;
            var zMax = float4.zero;
            for (var col = 0; col < Constants.NumColsSubTile; col++)
            {
                var start1 = startX + col * Constants.SubTileWidth;
                
                // var leftBottom = new int2(start1, startY) * Constants.SubPixelPrecision;
                // var rightTop = new int2(
                //     start1 + Constants.SubTileWidth - 1, 
                //     startY + Constants.SubTileHeight - 1
                // ) * Constants.SubPixelPrecision;
                // if (!IsPointInTriangle(leftBottom, v0, v1, v2) && !IsPointInTriangle(rightTop, v0, v1, v2))
                //     continue;
                
                for (var subRow = 0; subRow < Constants.SubTileHeight; subRow++)
                {
                    for (var subCol = 0; subCol < Constants.SubTileWidth; subCol++)
                    {
                        var maskIdx = subRow * Constants.SubTileWidth + subCol;
                        var p = new int2(
                            start1 + subCol,
                            startY + subRow
                        ) * Constants.SubPixelPrecision;
                        var z = z0 + zPixelDx * (p.x - v0.x) + zPixelDy * (p.y - v0.y);
                        zMax[col] = math.max(zMax[col], z);
                        if (IsPointInTriangle(p, v0, v1, v2))
                        {
                            bitmask[col] |= (uint)(1 << (31 - maskIdx));
                        }
                    }
                }
            }
            UpdateTile(tileIdx, ref bitmask, ref zMax);
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
        
        private void UpdateTile(int tileIdx, ref uint4 bitmask, ref float4 zMax)
        {
            tiles[tileIdx].bitmask |= bitmask;
            tiles[tileIdx].z1 = math.max(tiles[tileIdx].z1, zMax);
            var flags = tiles[tileIdx].bitmask == uint.MaxValue;
            for (var i = 0; i < 4; i++)
            {
                if (!flags[i]) continue;
                tiles[tileIdx].z0[i] = tiles[tileIdx].z1[i];
                tiles[tileIdx].z1[i] = 0.0f;
                tiles[tileIdx].bitmask[i] = 0u;
            }
        }
    }
}
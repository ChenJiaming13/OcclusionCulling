using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

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
            foreach (var meshFilter in meshFilters)
            {
                RenderMesh(meshFilter);
            }
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
                for (var i = 0; i < idxTri - startIdxTri; i++)
                {
                    var v0 = new int2(iVtxX[0][i], iVtxY[0][i]);
                    var v1 = new int2(iVtxX[1][i], iVtxY[1][i]);
                    var v2 = new int2(iVtxX[2][i], iVtxY[2][i]);
                    var bbRange = new int4(bbTileMinX[i], bbTileMaxX[i], bbTileMinY[i], bbTileMaxY[i]);
                    RasterizeTriangle(v0, v1, v2, bbRange);
                }
            }
            Debug.Log($"NumTri: {numTris} DONE!");
        }
        
        private void InitTiles()
        {
            tiles = new Tile[Constants.NumRowsTile * Constants.NumColsTile];
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
        
        private void RasterizeTriangle(int2 v0, int2 v1, int2 v2, int4 bbRange)
        {
            for (var tileX = bbRange.x; tileX <= bbRange.y; tileX++)
            {
                for (var tileY = bbRange.z; tileY <= bbRange.w; tileY++)
                {
                    UpdateTile(tileX, tileY, v0, v1, v2);
                }
            }
        }

        private void UpdateTile(int tileX, int tileY, int2 v0, int2 v1, int2 v2)
        {
            var startX = tileX * Constants.TileWidth;
            var startY = tileY * Constants.TileHeight;
            var tileIdx = tileY * Constants.NumColsTile + tileX;
            for (var col = 0; col < Constants.NumColsSubTile; col++)
            {
                var start1 = startX + col * Constants.SubTileWidth;
                uint subTileBitmask = 0;
                    
                for (var subRow = 0; subRow < Constants.SubTileHeight; subRow++)
                {
                    for (var subCol = 0; subCol < Constants.SubTileWidth; subCol++)
                    {
                        var maskIdx = subRow * Constants.SubTileWidth + subCol;
                        var p = new int2(
                            start1 + subCol,
                            startY + subRow
                        ) * Constants.SubPixelPrecision;
                        if (IsPointInTriangle(p, v0, v1, v2))
                        {
                            subTileBitmask |= (uint)(1 << (31 - maskIdx));
                        }
                    }
                }

                tiles[tileIdx].bitmask[col] |= subTileBitmask;
            }
        }
    }
}
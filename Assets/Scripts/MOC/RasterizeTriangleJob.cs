using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MOC
{
    [BurstCompile]
    public struct RasterizeTriangleJob : IJobParallelFor
    {
        public int4 TileRange;
        public int2 V0;
        public int2 V1;
        public int2 V2;
        public float Z0;
        public float ZPixelDx;
        public float ZPixelDy;
        [ReadOnly] public NativeArray<Tile> InputTiles;
        [WriteOnly] public NativeArray<Tile> OutputTiles;
        
        public void Execute(int index)
        {
            RasterizeTile(index);
        }
        
        private void RasterizeTile(int index)
        {
            var width = TileRange.y - TileRange.x + 1;
            var tileY = TileRange.z + index / width;
            var tileX = TileRange.x + index % width;
            
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
                        var z = Z0 + ZPixelDx * (p.x - V0.x) + ZPixelDy * (p.y - V0.y);
                        zMax[col] = math.max(zMax[col], z);
                        if (IsPointInTriangle(p, V0, V1, V2))
                        {
                            bitmask[col] |= (uint)(1 << (31 - maskIdx));
                        }
                    }
                }
            }
            UpdateTile(index, tileIdx, ref bitmask, ref zMax);
        }
        
        private void UpdateTile(int tileIdx, int globalTileIdx, ref uint4 bitmask, ref float4 zMax)
        {
            // Debug.Log($"{tileIdx}");
            var tile = InputTiles[globalTileIdx];
            tile.bitmask |= bitmask;
            tile.z1 = math.max(tile.z1, zMax);
            var flags = tile.bitmask == uint.MaxValue;
            for (var i = 0; i < 4; i++)
            {
                if (!flags[i]) continue;
                tile.z0[i] = tile.z1[i];
                tile.z1[i] = 0.0f;
                tile.bitmask[i] = 0u;
            }
            OutputTiles[tileIdx] = tile;
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
    }
}
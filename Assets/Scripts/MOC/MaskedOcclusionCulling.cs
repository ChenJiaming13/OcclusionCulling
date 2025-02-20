using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions;

namespace MOC
{
    public class MaskedOcclusionCulling : MonoBehaviour
    {
        [SerializeField] private Vector2Int resolution = new(1920, 1080);
        [SerializeField] private Tile[] tiles;

        public Tile[] Tiles => tiles;
        public int BufferWidth { get; private set; }
        public int BufferHeight { get; private set; }
        public int TilesWidth { get; private set; }
        public int TilesHeight { get; private set; }

        private void Start()
        {
            InitTiles();
        }

        private void InitTiles()
        {
            Assert.IsTrue(resolution.x % (Constants.SubTileWidth * Constants.NumColsSubTileInTile) == 0 &&
                          resolution.y % (Constants.SubTileHeight * Constants.NumRowsSubTileInTile) == 0);
            BufferWidth = resolution.x;
            BufferHeight = resolution.y;
            TilesWidth = BufferWidth / (Constants.SubTileWidth * Constants.NumColsSubTileInTile);
            TilesHeight = BufferHeight / (Constants.SubTileHeight * Constants.NumRowsSubTileInTile);
            tiles = new Tile[TilesWidth * TilesHeight];
            
            for (var i = 0; i < tiles.Length; i++)
            {
                tiles[i].bitmask = new uint4x2(
                    1, 31, 3, 63,
                    7, 127, 15, 255
                );
            }
        }
    }
    
    [Serializable]
    public struct Tile // contain 4x2 SubTiles
    {
        public float4x2 z0;
        public float4x2 z1;
        public uint4x2 bitmask;
    }
}
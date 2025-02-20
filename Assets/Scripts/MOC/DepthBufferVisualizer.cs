using System.IO;
using UnityEngine;

namespace MOC
{
    [RequireComponent(typeof(MaskedOcclusionCulling))]
    public class DepthBufferVisualizer : MonoBehaviour
    {
        [SerializeField] private Texture2D depthBuffer;
        [SerializeField] private Color z0Color = Color.black;
        [SerializeField] private Color z1Color = Color.white;
        private MaskedOcclusionCulling _moc;

        private void Start()
        {
            _moc = GetComponent<MaskedOcclusionCulling>();
        }

        [ContextMenu("Visualize")]
        private void Visualize()
        {
            CreateDepthBufferIfNeeded();
            UpdateDepthBuffer();
            SaveTextureAsPNG("Assets/test.png");
        }

        private void CreateDepthBufferIfNeeded()
        {
            if (depthBuffer == null || depthBuffer.width != _moc.BufferWidth || depthBuffer.height != _moc.BufferHeight)
            {
                depthBuffer = new Texture2D(_moc.BufferWidth, _moc.BufferHeight, TextureFormat.ARGB32, false)
                {
                    filterMode = FilterMode.Point
                };
            }
        }

        private void UpdateDepthBuffer()
        {
            for (var i = 0; i < _moc.Tiles.Length; i++)
            {
                var tileRow = i / _moc.TilesWidth;
                var tileCol = i % _moc.TilesWidth;
                UpdateTile(tileRow, tileCol, _moc.Tiles[i]);
            }
        }

        private void UpdateTile(int tileRow, int tileCol, in Tile tile)
        {
            for (var subTileRow = 0; subTileRow < Constants.NumRowsSubTileInTile; subTileRow++)
            {
                for (var subTileCol = 0; subTileCol < Constants.NumColsSubTileInTile; subTileCol++)
                {
                    var pixelRowStart = tileRow * Constants.NumRowsSubTileInTile * Constants.SubTileHeight +
                                        subTileRow * Constants.SubTileHeight;
                    var pixelColStart = tileCol * Constants.NumColsSubTileInTile * Constants.SubTileWidth +
                                        subTileCol * Constants.SubTileWidth;
                    UpdateSubTile(pixelRowStart, pixelColStart, tile.bitmask[subTileRow][subTileCol],
                        tile.z0[subTileRow][subTileCol], tile.z1[subTileRow][subTileCol]);
                }
            }
        }

        private void UpdateSubTile(int pixelRowStart, int pixelColStart, uint bitmask, float z0, float z1)
        {
            for (var row = 0; row < Constants.SubTileHeight; row++)
            {
                for (var col = 0; col < Constants.SubTileWidth; col++)
                {
                    var idx = row * Constants.SubTileWidth + col;
                    var bitValue = (bitmask >> (31 - idx)) & 1; 
                    var pixelRow = pixelRowStart + row;
                    var pixelCol = pixelColStart + col;
                    depthBuffer.SetPixel(pixelCol, pixelRow, bitValue == 1 ? z1Color : z0Color);
                }
            }
        }
        
        private void SaveTextureAsPNG(string path)
        {
            var pngBytes = depthBuffer.EncodeToPNG();
            File.WriteAllBytes(path, pngBytes);
            Debug.Log("Texture saved as PNG to: " + path);
        }
    }
}
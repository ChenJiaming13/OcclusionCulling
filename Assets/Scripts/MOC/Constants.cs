namespace MOC
{
    public static class Constants
    {
        public const int ScreenWidth = 1920;
        public const int ScreenHeight = 1080;
        public const int TileWidth = 32;
        public const int TileHeight = 4;
        public const int NumRowsTile = 270; // ScreenWidth / TileWidth
        public const int NumColsTile = 60; // ScreenHeight / TileHeight
        public const int SubTileWidth = 8;
        public const int SubTileHeight = 4;
        public const int NumColsSubTile = 4; // TileWidth / SubTileWidth
        public const int NumRowsSubTile = 1; // TileHeight / SubTileHeight
        public const int SubPixelPrecision = 1;
    }
}
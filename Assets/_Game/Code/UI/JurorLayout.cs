using UnityEngine;

namespace Game.UI
{
    public static class JurorLayout
    {
        public const int Width = 200;
        public const int Height = 16;
        public const int Rows = 3;
        public const int RowStep = 2;
        public const int FirstRowY = 6;
        public const int ColumnsPerGroup = 10;
        public const int GroupGap = 1;
        public const int MarkerLowY = 1;
        public const int MarkerHighY = 14;
        public const int MarkerLength = 3;

        public static Vector2Int AcquittalPixel(int order) => new(ColumnX(order / Rows), RowY(order % Rows));

        public static Vector2Int CondemnationPixel(int order) => new(Width - 1 - ColumnX(order / Rows), RowY(order % Rows));

        public static int ColumnX(int column) => column + column / ColumnsPerGroup * GroupGap;

        private static int RowY(int row) => FirstRowY + row * RowStep;
    }
}

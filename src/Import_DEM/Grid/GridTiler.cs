using System;
using System.Collections.Generic;

namespace Import_DEM.Grid
{
    /// <summary>A rectangle of samples that becomes one surface.</summary>
    public readonly struct GridTile
    {
        public GridTile(int columnStart, int rowStart, int columnCount, int rowCount)
        {
            ColumnStart = columnStart;
            RowStart = rowStart;
            ColumnCount = columnCount;
            RowCount = rowCount;
        }

        public int ColumnStart { get; }

        public int RowStart { get; }

        public int ColumnCount { get; }

        public int RowCount { get; }

        public int PointCount => ColumnCount * RowCount;
    }

    /// <summary>
    /// Splits a grid into tiles that each become one surface. Neighbour tiles share one row or one
    /// column of samples, so the surfaces meet edge to edge with no gap.
    /// </summary>
    public static class GridTiler
    {
        /// <summary>The smallest tile side. A tile of one sample has no surface.</summary>
        public const int MinimumPatchSize = 2;

        public static IReadOnlyList<GridTile> Split(int columns, int rows, int maxPatchSize)
        {
            if (columns < 2 || rows < 2)
                throw new ArgumentOutOfRangeException(
                    nameof(columns),
                    $"The grid is {columns} by {rows} samples. A surface needs at least 2 samples in each direction.");

            var columnRanges = SplitAxis(columns, maxPatchSize);
            var rowRanges = SplitAxis(rows, maxPatchSize);

            var tiles = new List<GridTile>(columnRanges.Count * rowRanges.Count);
            foreach (var rowRange in rowRanges)
            {
                foreach (var columnRange in columnRanges)
                    tiles.Add(new GridTile(columnRange.Start, rowRange.Start, columnRange.Count, rowRange.Count));
            }

            return tiles;
        }

        /// <summary>Counts the tiles without building them. The options dialog shows the count.</summary>
        public static int CountTiles(int columns, int rows, int maxPatchSize)
        {
            if (columns < 2 || rows < 2)
                return 0;

            return SplitAxis(columns, maxPatchSize).Count * SplitAxis(rows, maxPatchSize).Count;
        }

        /// <summary>
        /// Cuts one axis into ranges of at most <paramref name="maxPatchSize"/> samples. Each range
        /// starts on the last sample of the range before it, which is the shared edge.
        /// </summary>
        private static IReadOnlyList<(int Start, int Count)> SplitAxis(int length, int maxPatchSize)
        {
            var patchSize = Math.Max(MinimumPatchSize, maxPatchSize);
            var ranges = new List<(int Start, int Count)>();

            var start = 0;
            while (start < length - 1)
            {
                var count = Math.Min(patchSize, length - start);
                ranges.Add((start, count));
                start += count - 1;
            }

            return ranges;
        }
    }
}

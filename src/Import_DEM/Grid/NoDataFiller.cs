using System;

namespace Import_DEM.Grid
{
    /// <summary>
    /// Closes the voids of an elevation grid. A NURBS surface is a full grid of points, so a void
    /// needs an elevation before the surface can carry it.
    /// </summary>
    public static class NoDataFiller
    {
        /// <summary>
        /// Replaces every void by the mean of its valid neighbours. The fill runs in passes: each
        /// pass fills the voids that touch a valid sample, so the values grow inward from the edge
        /// of each void until the void is closed.
        /// </summary>
        /// <returns>The count of the samples that the fill replaced.</returns>
        public static int Fill(ElevationGrid grid)
        {
            var isValid = MarkValidSamples(grid, out var voidCount, out var validSum, out var validCount);
            if (voidCount == 0)
                return 0;

            if (validCount == 0)
            {
                // Every sample is a void. There is nothing to grow the values from.
                FillAll(grid, isValid, 0.0);
                return voidCount;
            }

            var remaining = voidCount;
            while (remaining > 0)
            {
                var filled = FillOnePass(grid, isValid);
                if (filled == 0)
                    break;

                remaining -= filled;
            }

            // A void that no pass reached cannot touch any valid sample, so it takes the grid mean.
            if (remaining > 0)
                FillAll(grid, isValid, validSum / validCount);

            return voidCount;
        }

        /// <summary>Replaces every void by one elevation.</summary>
        /// <returns>The count of the samples that the fill replaced.</returns>
        public static int FillWithConstant(ElevationGrid grid, double elevation)
        {
            var isValid = MarkValidSamples(grid, out var voidCount, out _, out _);
            if (voidCount > 0)
                FillAll(grid, isValid, elevation);

            return voidCount;
        }

        /// <summary>
        /// Records which samples hold an elevation. The fill works on this map and not on the
        /// sample values, so a filled value that matches the void marker stays filled.
        /// </summary>
        private static bool[] MarkValidSamples(ElevationGrid grid, out int voidCount, out double validSum, out int validCount)
        {
            var isValid = new bool[grid.Columns * grid.Rows];
            voidCount = 0;
            validSum = 0.0;
            validCount = 0;

            for (var row = 0; row < grid.Rows; row++)
            {
                for (var column = 0; column < grid.Columns; column++)
                {
                    if (grid.IsVoid(column, row))
                    {
                        voidCount++;
                        continue;
                    }

                    isValid[row * grid.Columns + column] = true;
                    validSum += grid.GetSample(column, row);
                    validCount++;
                }
            }

            return isValid;
        }

        /// <summary>
        /// Fills every void that touches a valid sample. The new values land after the pass, so the
        /// result does not depend on the order of the samples.
        /// </summary>
        private static int FillOnePass(ElevationGrid grid, bool[] isValid)
        {
            var filledColumns = new int[grid.Columns * grid.Rows];
            var filledRows = new int[grid.Columns * grid.Rows];
            var filledValues = new double[grid.Columns * grid.Rows];
            var filledCount = 0;

            for (var row = 0; row < grid.Rows; row++)
            {
                for (var column = 0; column < grid.Columns; column++)
                {
                    if (isValid[row * grid.Columns + column])
                        continue;

                    if (!TryAverageNeighbours(grid, isValid, column, row, out var elevation))
                        continue;

                    filledColumns[filledCount] = column;
                    filledRows[filledCount] = row;
                    filledValues[filledCount] = elevation;
                    filledCount++;
                }
            }

            for (var index = 0; index < filledCount; index++)
            {
                var column = filledColumns[index];
                var row = filledRows[index];
                grid.SetSample(column, row, filledValues[index]);
                isValid[row * grid.Columns + column] = true;
            }

            return filledCount;
        }

        /// <summary>Averages the valid samples of the eight neighbours.</summary>
        private static bool TryAverageNeighbours(ElevationGrid grid, bool[] isValid, int column, int row, out double elevation)
        {
            var sum = 0.0;
            var count = 0;

            var firstRow = Math.Max(0, row - 1);
            var lastRow = Math.Min(grid.Rows - 1, row + 1);
            var firstColumn = Math.Max(0, column - 1);
            var lastColumn = Math.Min(grid.Columns - 1, column + 1);

            for (var neighbourRow = firstRow; neighbourRow <= lastRow; neighbourRow++)
            {
                for (var neighbourColumn = firstColumn; neighbourColumn <= lastColumn; neighbourColumn++)
                {
                    if (neighbourColumn == column && neighbourRow == row)
                        continue;

                    if (!isValid[neighbourRow * grid.Columns + neighbourColumn])
                        continue;

                    sum += grid.GetSample(neighbourColumn, neighbourRow);
                    count++;
                }
            }

            elevation = count > 0 ? sum / count : 0.0;
            return count > 0;
        }

        private static void FillAll(ElevationGrid grid, bool[] isValid, double elevation)
        {
            for (var row = 0; row < grid.Rows; row++)
            {
                for (var column = 0; column < grid.Columns; column++)
                {
                    if (isValid[row * grid.Columns + column])
                        continue;

                    grid.SetSample(column, row, elevation);
                    isValid[row * grid.Columns + column] = true;
                }
            }
        }
    }
}

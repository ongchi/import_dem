using System;

namespace Import_DEM.Grid
{
    /// <summary>
    /// A regular grid of elevation samples. The samples are row major from the top left corner,
    /// as the source raster holds them. The X of a column grows to the right and the Y of a row
    /// falls downward, which is the orientation of every north up raster.
    /// </summary>
    public sealed class ElevationGrid
    {
        private readonly double[] _samples;

        public ElevationGrid(
            double[] samples,
            int columns,
            int rows,
            double firstSampleX,
            double firstSampleY,
            double stepX,
            double stepY,
            double? noDataValue)
        {
            if (columns < 2 || rows < 2)
                throw new ArgumentOutOfRangeException(
                    nameof(columns),
                    $"The grid is {columns} by {rows} samples. A surface needs at least 2 samples in each direction.");

            if (samples.Length != (long)columns * rows)
                throw new ArgumentException(
                    $"The grid holds {samples.Length} samples but {columns} by {rows} needs {(long)columns * rows}.",
                    nameof(samples));

            _samples = samples;
            Columns = columns;
            Rows = rows;
            FirstSampleX = firstSampleX;
            FirstSampleY = firstSampleY;
            StepX = stepX;
            StepY = stepY;
            NoDataValue = noDataValue;
        }

        public int Columns { get; }

        public int Rows { get; }

        /// <summary>The X of the center of the sample in the first column.</summary>
        public double FirstSampleX { get; }

        /// <summary>The Y of the center of the sample in the first row.</summary>
        public double FirstSampleY { get; }

        /// <summary>The distance between two columns.</summary>
        public double StepX { get; }

        /// <summary>The distance between two rows.</summary>
        public double StepY { get; }

        /// <summary>The value that marks a void, or null when the grid has no void marker.</summary>
        public double? NoDataValue { get; }

        public long SampleCount => (long)Columns * Rows;

        public double GetSample(int column, int row) => _samples[(long)row * Columns + column];

        public void SetSample(int column, int row, double elevation) => _samples[(long)row * Columns + column] = elevation;

        /// <summary>True when the sample is a void.</summary>
        public bool IsVoid(int column, int row) => IsVoidValue(GetSample(column, row));

        public bool IsVoidValue(double elevation)
        {
            if (double.IsNaN(elevation))
                return true;

            if (NoDataValue is not { } noDataValue)
                return false;

            // A no data marker of NaN matches every NaN sample, which the test above already caught.
            return !double.IsNaN(noDataValue) && elevation == noDataValue;
        }

        public double XAt(int column) => FirstSampleX + column * StepX;

        public double YAt(int row) => FirstSampleY - row * StepY;

        /// <summary>The X of the center of the grid.</summary>
        public double CenterX => FirstSampleX + (Columns - 1) * StepX / 2.0;

        /// <summary>The Y of the center of the grid.</summary>
        public double CenterY => FirstSampleY - (Rows - 1) * StepY / 2.0;

        public int CountVoids()
        {
            var count = 0;
            foreach (var sample in _samples)
            {
                if (IsVoidValue(sample))
                    count++;
            }

            return count;
        }
    }
}

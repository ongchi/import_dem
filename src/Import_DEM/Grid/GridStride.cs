using System;

namespace Import_DEM.Grid
{
    /// <summary>
    /// Chooses how much of a raster to sample. A full DEM holds far more samples than a document of
    /// NURBS surfaces can carry, so the import keeps one sample in every N.
    /// </summary>
    public static class GridStride
    {
        /// <summary>
        /// The sample budget of the interpolated mode. The solve that fits a surface through the
        /// samples is heavy, so this mode takes fewer samples than the approximated mode.
        /// </summary>
        public const long InterpolatedSampleBudget = 250_000;

        /// <summary>The sample budget of the approximated mode, where the samples are control points.</summary>
        public const long ApproximatedSampleBudget = 1_000_000;

        /// <summary>
        /// Suggests the stride that brings the raster under the budget. A raster that already fits
        /// keeps every sample, so the suggestion is 1.
        /// </summary>
        public static int Suggest(int width, int height, long sampleBudget)
        {
            if (width < 2 || height < 2 || sampleBudget < 4)
                return 1;

            var stride = 1;
            while ((long)SampledLength(width, stride) * SampledLength(height, stride) > sampleBudget)
                stride++;

            return stride;
        }

        /// <summary>
        /// The sample count of one axis after the stride. Two samples is the smallest grid that
        /// carries a surface, so the result never falls below it.
        /// </summary>
        public static int SampledLength(int length, int stride)
        {
            if (stride < 1)
                throw new ArgumentOutOfRangeException(nameof(stride), "The stride must be 1 or more.");

            return Math.Max(2, (int)Math.Ceiling(length / (double)stride));
        }
    }
}

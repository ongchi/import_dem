using Import_DEM.Grid;
using Xunit;

namespace Import_DEM.Tests
{
    public class GridStrideTests
    {
        [Fact]
        public void Raster_that_fits_the_budget_keeps_every_sample()
        {
            Assert.Equal(1, GridStride.Suggest(400, 300, GridStride.InterpolatedSampleBudget));
        }

        [Fact]
        public void Large_raster_gets_a_stride_that_meets_the_budget()
        {
            var stride = GridStride.Suggest(10000, 10000, GridStride.InterpolatedSampleBudget);

            var columns = GridStride.SampledLength(10000, stride);
            var rows = GridStride.SampledLength(10000, stride);

            Assert.True(stride > 1);
            Assert.True((long)columns * rows <= GridStride.InterpolatedSampleBudget);
        }

        [Fact]
        public void Stride_is_the_smallest_one_that_meets_the_budget()
        {
            var stride = GridStride.Suggest(10000, 10000, GridStride.InterpolatedSampleBudget);

            var previous = stride - 1;
            var columns = GridStride.SampledLength(10000, previous);
            var rows = GridStride.SampledLength(10000, previous);

            Assert.True((long)columns * rows > GridStride.InterpolatedSampleBudget);
        }

        [Fact]
        public void Approximated_mode_takes_more_samples_than_interpolated_mode()
        {
            var interpolated = GridStride.Suggest(10000, 10000, GridStride.InterpolatedSampleBudget);
            var approximated = GridStride.Suggest(10000, 10000, GridStride.ApproximatedSampleBudget);

            Assert.True(approximated < interpolated);
        }

        [Fact]
        public void Sampled_length_rounds_upward()
        {
            Assert.Equal(50, GridStride.SampledLength(100, 2));
            Assert.Equal(34, GridStride.SampledLength(100, 3));
            Assert.Equal(100, GridStride.SampledLength(100, 1));
        }

        [Fact]
        public void Sampled_length_never_falls_below_two_samples()
        {
            Assert.Equal(2, GridStride.SampledLength(10, 1000));
        }
    }
}

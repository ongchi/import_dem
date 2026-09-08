using Import_DEM.Formats;
using Import_DEM.Grid;
using Xunit;

namespace Import_DEM.Tests
{
    public class NoDataFillerTests
    {
        private const double NoData = -9999.0;

        /// <summary>Builds a grid of a constant elevation, with the listed samples marked as voids.</summary>
        private static ElevationGrid BuildGrid(int columns, int rows, double elevation, params (int Column, int Row)[] voids)
        {
            var samples = new double[columns * rows];
            for (var index = 0; index < samples.Length; index++)
                samples[index] = elevation;

            var grid = new ElevationGrid(samples, columns, rows, 0.0, 0.0, 1.0, 1.0, NoData);
            foreach (var (column, row) in voids)
                grid.SetSample(column, row, NoData);

            return grid;
        }

        [Fact]
        public void Grid_without_a_void_stays_unchanged()
        {
            var grid = BuildGrid(5, 5, 100.0);

            Assert.Equal(0, NoDataFiller.Fill(grid));
            Assert.Equal(100.0, grid.GetSample(2, 2), 9);
        }

        [Fact]
        public void Fill_closes_every_void()
        {
            var grid = EsriGridReader.Read(Fixtures.Path("hill_voids.bil"));
            Assert.Equal(9, grid.CountVoids());

            var filled = NoDataFiller.Fill(grid);

            Assert.Equal(9, filled);
            Assert.Equal(0, grid.CountVoids());
        }

        [Fact]
        public void Filled_sample_takes_the_mean_of_its_valid_neighbours()
        {
            var grid = BuildGrid(5, 5, 100.0, (2, 2));

            NoDataFiller.Fill(grid);

            Assert.Equal(100.0, grid.GetSample(2, 2), 9);
        }

        [Fact]
        public void Fill_reaches_a_void_that_touches_no_valid_sample_at_the_start()
        {
            // A 3 by 3 void: the center touches only voids until the first pass fills the ring.
            var grid = BuildGrid(
                7, 7, 100.0,
                (2, 2), (3, 2), (4, 2),
                (2, 3), (3, 3), (4, 3),
                (2, 4), (3, 4), (4, 4));

            var filled = NoDataFiller.Fill(grid);

            Assert.Equal(9, filled);
            Assert.Equal(0, grid.CountVoids());
            Assert.Equal(100.0, grid.GetSample(3, 3), 9);
        }

        [Fact]
        public void Fill_does_not_depend_on_the_order_of_the_samples()
        {
            // The fill of one pass reads the values from before the pass, so a slope stays a slope.
            var samples = new double[3 * 3];
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                    samples[row * 3 + column] = column * 10.0;
            }

            var grid = new ElevationGrid(samples, 3, 3, 0.0, 0.0, 1.0, 1.0, NoData);
            grid.SetSample(1, 1, NoData);

            NoDataFiller.Fill(grid);

            Assert.Equal(10.0, grid.GetSample(1, 1), 9);
        }

        [Fact]
        public void Grid_of_only_voids_fills_with_zero()
        {
            var grid = BuildGrid(3, 3, NoData);

            var filled = NoDataFiller.Fill(grid);

            Assert.Equal(9, filled);
            Assert.Equal(0, grid.CountVoids());
            Assert.Equal(0.0, grid.GetSample(1, 1), 9);
        }

        [Fact]
        public void Constant_fill_writes_the_given_elevation()
        {
            var grid = BuildGrid(5, 5, 100.0, (1, 1), (3, 3));

            var filled = NoDataFiller.FillWithConstant(grid, 42.0);

            Assert.Equal(2, filled);
            Assert.Equal(42.0, grid.GetSample(1, 1), 9);
            Assert.Equal(42.0, grid.GetSample(3, 3), 9);
            Assert.Equal(100.0, grid.GetSample(0, 0), 9);
        }

        [Fact]
        public void Sample_that_is_not_a_number_counts_as_a_void()
        {
            var grid = BuildGrid(5, 5, 100.0);
            grid.SetSample(2, 2, double.NaN);

            Assert.True(grid.IsVoid(2, 2));
            Assert.Equal(1, NoDataFiller.Fill(grid));
            Assert.Equal(100.0, grid.GetSample(2, 2), 9);
        }

        [Fact]
        public void Fill_ends_when_the_new_value_matches_the_void_marker()
        {
            // The neighbours of the void average to exactly the marker value. The fill tracks which
            // samples it wrote, and not the sample values, so it writes this sample one time only.
            var grid = BuildGrid(3, 3, NoData - 1.0, (1, 1));
            grid.SetSample(0, 0, NoData + 1.0);
            grid.SetSample(1, 0, NoData + 1.0);
            grid.SetSample(2, 0, NoData + 1.0);
            grid.SetSample(0, 1, NoData + 1.0);

            var filled = NoDataFiller.Fill(grid);

            Assert.Equal(1, filled);
            Assert.Equal(NoData, grid.GetSample(1, 1), 9);
        }
    }
}

using Import_DEM.Formats;
using Xunit;

namespace Import_DEM.Tests
{
    public class EsriGridReaderTests
    {
        [Fact]
        public void Reader_reports_the_grid_size()
        {
            var grid = EsriGridReader.Read(Fixtures.Path("hill.bil"));

            Assert.Equal(40, grid.Columns);
            Assert.Equal(30, grid.Rows);
            Assert.Equal(1200, grid.SampleCount);
        }

        [Fact]
        public void First_sample_sits_at_the_center_that_the_header_gives()
        {
            var grid = EsriGridReader.Read(Fixtures.Path("hill.bil"));

            Assert.Equal(500012.5, grid.XAt(0), 6);
            Assert.Equal(4600737.5, grid.YAt(0), 6);
        }

        [Fact]
        public void Column_position_grows_to_the_right_and_row_position_falls_downward()
        {
            var grid = EsriGridReader.Read(Fixtures.Path("hill.bil"));

            Assert.Equal(500037.5, grid.XAt(1), 6);
            Assert.Equal(4600712.5, grid.YAt(1), 6);
        }

        [Fact]
        public void Samples_are_row_major_from_the_top_left()
        {
            var grid = EsriGridReader.Read(Fixtures.Path("hill.bil"));

            // The fixture holds 1200 + 80 * sin(column / 6) * cos(row / 5).
            Assert.Equal(1200.0, grid.GetSample(0, 0), 3);
            Assert.Equal(1213.2717, grid.GetSample(1, 0), 3);
            Assert.Equal(1200.0, grid.GetSample(0, 1), 3);
        }

        [Fact]
        public void Reader_reads_the_no_data_value_of_the_header()
        {
            var grid = EsriGridReader.Read(Fixtures.Path("hill_voids.bil"));

            Assert.Equal(-9999.0, grid.NoDataValue);
            Assert.True(grid.IsVoid(0, 0));
            Assert.False(grid.IsVoid(10, 10));
        }

        [Fact]
        public void Void_count_matches_the_marked_block()
        {
            var grid = EsriGridReader.Read(Fixtures.Path("hill_voids.bil"));

            Assert.Equal(9, grid.CountVoids());
        }

        [Fact]
        public void Reader_reads_big_endian_samples()
        {
            var grid = EsriGridReader.Read(Fixtures.Path("big_endian.bil"));

            Assert.Equal(1200.0, grid.GetSample(0, 0), 3);
            Assert.Equal(1213.2717, grid.GetSample(1, 0), 3);
        }

        [Fact]
        public void Strided_grid_keeps_the_georeference_of_its_header()
        {
            // GDAL rewrites the header after -outsize, so the reader takes the step from the file.
            var grid = EsriGridReader.Read(Fixtures.Path("hill_strided.bil"));

            Assert.Equal(20, grid.Columns);
            Assert.Equal(15, grid.Rows);
            Assert.Equal(50.0, grid.StepX, 6);
            Assert.Equal(500025.0, grid.XAt(0), 6);
            Assert.Equal(500075.0, grid.XAt(1), 6);
        }

        [Fact]
        public void Grid_center_sits_between_the_first_and_the_last_sample()
        {
            var grid = EsriGridReader.Read(Fixtures.Path("hill.bil"));

            Assert.Equal(500500.0, grid.CenterX, 6);
            Assert.Equal(4600375.0, grid.CenterY, 6);
        }

        [Fact]
        public void Truncated_block_is_refused()
        {
            var exception = Assert.Throws<GridFormatException>(() => EsriGridReader.Read(Fixtures.Path("truncated.bil")));

            Assert.Contains("4800", exception.Message);
        }

        [Fact]
        public void Sample_type_other_than_float_is_refused()
        {
            var exception = Assert.Throws<GridFormatException>(() => EsriGridReader.Read(Fixtures.Path("wrong_type.bil")));

            Assert.Contains("PIXELTYPE", exception.Message);
        }

        [Fact]
        public void More_than_one_band_is_refused()
        {
            var exception = Assert.Throws<GridFormatException>(() => EsriGridReader.Read(Fixtures.Path("two_bands.bil")));

            Assert.Contains("NBANDS", exception.Message);
        }
    }
}

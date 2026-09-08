using Import_DEM.Gdal;
using Xunit;

namespace Import_DEM.Tests
{
    public class GdalInfoReaderTests
    {
        private static GdalInfo Parse(string fixtureName)
        {
            return GdalInfoReader.Parse(Fixtures.ReadText(fixtureName), "probe.img");
        }

        [Fact]
        public void Reader_reports_the_driver_and_the_raster_size()
        {
            var info = Parse("gdalinfo_hfa.json");

            Assert.Equal("HFA", info.DriverShortName);
            Assert.Equal(40, info.Width);
            Assert.Equal(30, info.Height);
            Assert.Equal(1200, info.SampleCount);
        }

        [Fact]
        public void Reader_reports_the_geo_transform()
        {
            var info = Parse("gdalinfo_hfa.json");

            Assert.Equal(500000.0, info.GeoTransform[0], 6);
            Assert.Equal(25.0, info.GeoTransform[1], 6);
            Assert.Equal(-25.0, info.GeoTransform[5], 6);
        }

        [Fact]
        public void North_up_raster_is_axis_aligned()
        {
            Assert.True(Parse("gdalinfo_hfa.json").IsAxisAligned);
        }

        [Fact]
        public void Rotated_raster_is_not_axis_aligned()
        {
            Assert.False(Parse("gdalinfo_rotated.json").IsAxisAligned);
        }

        [Fact]
        public void Reader_reads_the_coordinate_system_text()
        {
            var info = Parse("gdalinfo_hfa.json");

            Assert.NotNull(info.CoordinateSystemWkt);
            Assert.Contains("UTM zone 33N", info.CoordinateSystemWkt);
        }

        [Fact]
        public void Raster_without_a_coordinate_system_reads_with_no_text()
        {
            Assert.Null(Parse("gdalinfo_no_srs.json").CoordinateSystemWkt);
        }

        [Fact]
        public void Reader_reads_the_band_type_and_the_no_data_value()
        {
            var band = Parse("gdalinfo_hfa.json").Bands[0];

            Assert.Equal(1, band.Index);
            Assert.Equal("Float32", band.DataType);
            Assert.Equal(-9999.0, band.NoDataValue);
            Assert.Equal("Layer_1", band.Description);
        }

        [Fact]
        public void Band_without_a_no_data_value_reads_with_none()
        {
            Assert.Null(Parse("gdalinfo_rgb.json").Bands[0].NoDataValue);
        }

        [Fact]
        public void No_data_value_of_nan_reads_as_not_a_number()
        {
            // GDAL writes the JSON string "nan" and not a JSON number for this value.
            var noDataValue = Parse("gdalinfo_float64.json").Bands[0].NoDataValue;

            Assert.NotNull(noDataValue);
            Assert.True(double.IsNaN(noDataValue!.Value));
        }

        [Fact]
        public void Float64_band_reports_that_it_needs_more_than_float32()
        {
            Assert.True(Parse("gdalinfo_float64.json").Bands[0].NeedsMoreThanFloat32);
            Assert.False(Parse("gdalinfo_hfa.json").Bands[0].NeedsMoreThanFloat32);
        }

        [Fact]
        public void Reader_reads_every_band_of_a_colour_raster()
        {
            var info = Parse("gdalinfo_rgb.json");

            Assert.Equal(3, info.Bands.Count);
            Assert.Equal(2, info.FindBand(2)!.Index);
            Assert.Null(info.FindBand(4));
        }

        [Fact]
        public void Band_label_names_the_band_and_its_type()
        {
            Assert.Equal("Band 1 — Layer_1 (Float32)", Parse("gdalinfo_hfa.json").Bands[0].Label);
            Assert.Equal("Band 2 (Byte)", Parse("gdalinfo_rgb.json").Bands[1].Label);
        }

        [Fact]
        public void Output_that_is_not_json_is_refused()
        {
            Assert.Throws<GdalFailureException>(() => GdalInfoReader.Parse("ERROR 4: no such file", "probe.img"));
        }

        [Fact]
        public void Raster_smaller_than_two_samples_is_refused()
        {
            var json = "{\"size\":[1,1],\"bands\":[{\"band\":1,\"type\":\"Float32\"}]}";

            var exception = Assert.Throws<GdalFailureException>(() => GdalInfoReader.Parse(json, "probe.img"));

            Assert.Contains("at least 2 samples", exception.Message);
        }
    }
}

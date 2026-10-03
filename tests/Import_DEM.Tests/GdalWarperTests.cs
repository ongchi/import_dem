using System.Linq;
using Import_DEM.Gdal;
using Xunit;

namespace Import_DEM.Tests
{
    public class GdalWarperTests
    {
        [Fact]
        public void Arguments_write_a_vrt_in_the_target_crs()
        {
            var arguments = GdalWarper.BuildArguments("dem.img", null, "EPSG:25833", false, "warped.vrt");

            Assert.Equal(
                new[] { "-q", "-of", "VRT", "-r", "bilinear", "-t_srs", "EPSG:25833", "dem.img", "warped.vrt" },
                arguments.ToArray());
        }

        [Fact]
        public void Source_crs_override_goes_before_the_target_crs()
        {
            var arguments = GdalWarper.BuildArguments("dem.img", " EPSG:32633 ", "EPSG:25833", false, "warped.vrt").ToList();

            var sourceIndex = arguments.IndexOf("-s_srs");

            Assert.True(sourceIndex >= 0);
            Assert.Equal("EPSG:32633", arguments[sourceIndex + 1]);
            Assert.True(sourceIndex < arguments.IndexOf("-t_srs"));
        }

        [Fact]
        public void Empty_source_crs_gives_no_override()
        {
            var arguments = GdalWarper.BuildArguments("dem.img", "  ", "EPSG:25833", false, "warped.vrt");

            Assert.DoesNotContain("-s_srs", arguments);
        }

        [Fact]
        public void Source_without_a_no_data_value_gets_one()
        {
            var arguments = GdalWarper.BuildArguments("dem.img", null, "EPSG:25833", true, "warped.vrt").ToList();

            var noDataIndex = arguments.IndexOf("-dstnodata");

            Assert.True(noDataIndex >= 0);
            Assert.Equal("-9999", arguments[noDataIndex + 1]);
        }
    }
}

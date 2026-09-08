using System.Linq;
using Import_DEM.Grid;
using Xunit;

namespace Import_DEM.Tests
{
    public class GridTilerTests
    {
        [Fact]
        public void Grid_that_fits_the_patch_size_makes_one_tile()
        {
            var tiles = GridTiler.Split(40, 30, 100);

            var tile = Assert.Single(tiles);
            Assert.Equal(0, tile.ColumnStart);
            Assert.Equal(0, tile.RowStart);
            Assert.Equal(40, tile.ColumnCount);
            Assert.Equal(30, tile.RowCount);
        }

        [Fact]
        public void Large_grid_splits_into_tiles_of_at_most_the_patch_size()
        {
            var tiles = GridTiler.Split(100, 100, 30);

            Assert.All(tiles, tile => Assert.True(tile.ColumnCount <= 30 && tile.RowCount <= 30));
        }

        [Fact]
        public void Neighbour_tiles_share_one_column()
        {
            var tiles = GridTiler.Split(10, 4, 4).Where(tile => tile.RowStart == 0).ToList();

            // Ranges 0..3, 3..6, 6..9: each starts on the last column of the range before it.
            Assert.Equal(new[] { 0, 3, 6 }, tiles.Select(tile => tile.ColumnStart));
            Assert.Equal(new[] { 4, 4, 4 }, tiles.Select(tile => tile.ColumnCount));
        }

        [Fact]
        public void Tiles_cover_the_whole_grid()
        {
            var tiles = GridTiler.Split(37, 23, 8);

            Assert.Equal(0, tiles.Min(tile => tile.ColumnStart));
            Assert.Equal(0, tiles.Min(tile => tile.RowStart));
            Assert.Equal(37, tiles.Max(tile => tile.ColumnStart + tile.ColumnCount));
            Assert.Equal(23, tiles.Max(tile => tile.RowStart + tile.RowCount));
        }

        [Fact]
        public void Every_tile_holds_at_least_two_samples_in_each_direction()
        {
            var tiles = GridTiler.Split(37, 23, 8);

            Assert.All(tiles, tile => Assert.True(tile.ColumnCount >= 2 && tile.RowCount >= 2));
        }

        [Fact]
        public void Tile_count_matches_the_tiles_that_the_split_builds()
        {
            Assert.Equal(GridTiler.Split(100, 100, 30).Count, GridTiler.CountTiles(100, 100, 30));
            Assert.Equal(GridTiler.Split(37, 23, 8).Count, GridTiler.CountTiles(37, 23, 8));
            Assert.Equal(1, GridTiler.CountTiles(40, 30, 100));
        }

        [Fact]
        public void Patch_size_below_two_falls_back_to_two()
        {
            var tiles = GridTiler.Split(5, 5, 1);

            Assert.All(tiles, tile => Assert.Equal(2, tile.ColumnCount));
            Assert.Equal(16, tiles.Count);
        }

        [Fact]
        public void Grid_smaller_than_two_samples_is_refused()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => GridTiler.Split(1, 10, 8));
        }
    }
}

using System;
using Import_DEM.Grid;
using Rhino.Geometry;

namespace Import_DEM.Import
{
    /// <summary>Builds one NURBS surface from a rectangle of elevation samples.</summary>
    public static class SurfaceBuilder
    {
        /// <summary>The degree of a surface that holds enough samples for it.</summary>
        public const int PreferredDegree = 3;

        /// <summary>
        /// Builds the surface of one tile.
        /// The U direction follows the columns and the V direction follows the rows.
        /// </summary>
        /// <param name="grid">The samples.</param>
        /// <param name="tile">The rectangle of the grid that becomes this surface.</param>
        /// <param name="surfaceType">Through the samples, or from the samples as control points.</param>
        /// <param name="transform">The unit scale and the offset that place the tile in the document.</param>
        /// <param name="elevationFactor">The factor that turns a sample into a Z coordinate in the source unit.</param>
        /// <returns>The surface, or null when Rhino could not build it.</returns>
        public static NurbsSurface? Build(
            ElevationGrid grid,
            GridTile tile,
            SurfaceType surfaceType,
            PointTransform transform,
            double elevationFactor)
        {
            var points = BuildPoints(grid, tile, transform, elevationFactor);

            // A tile at the edge of the grid can be thinner than the preferred degree allows.
            var uDegree = DegreeFor(tile.ColumnCount);
            var vDegree = DegreeFor(tile.RowCount);

            return surfaceType == SurfaceType.Interpolated
                ? NurbsSurface.CreateThroughPoints(points, tile.ColumnCount, tile.RowCount, uDegree, vDegree, false, false)
                : NurbsSurface.CreateFromPoints(points, tile.ColumnCount, tile.RowCount, uDegree, vDegree);
        }

        /// <summary>
        /// The grid of points of one tile. The order is the RhinoCommon grid order: the V index,
        /// which is the row, runs fastest.
        /// </summary>
        private static Point3d[] BuildPoints(
            ElevationGrid grid,
            GridTile tile,
            PointTransform transform,
            double elevationFactor)
        {
            var points = new Point3d[tile.PointCount];
            var index = 0;

            for (var column = 0; column < tile.ColumnCount; column++)
            {
                var gridColumn = tile.ColumnStart + column;
                var x = grid.XAt(gridColumn);

                for (var row = 0; row < tile.RowCount; row++)
                {
                    var gridRow = tile.RowStart + row;
                    points[index++] = transform.Apply(
                        x,
                        grid.YAt(gridRow),
                        grid.GetSample(gridColumn, gridRow) * elevationFactor);
                }
            }

            return points;
        }

        /// <summary>A degree needs one more point than its value, so a thin tile takes a lower degree.</summary>
        public static int DegreeFor(int pointCount) => Math.Max(1, Math.Min(PreferredDegree, pointCount - 1));

        /// <summary>True when the tile holds a sample with no elevation.</summary>
        public static bool HoldsVoid(ElevationGrid grid, GridTile tile)
        {
            for (var row = 0; row < tile.RowCount; row++)
            {
                for (var column = 0; column < tile.ColumnCount; column++)
                {
                    if (grid.IsVoid(tile.ColumnStart + column, tile.RowStart + row))
                        return true;
                }
            }

            return false;
        }
    }
}

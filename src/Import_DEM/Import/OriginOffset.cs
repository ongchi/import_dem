using System;
using System.Globalization;
using Import_DEM.Grid;
using Rhino;
using Rhino.Geometry;

namespace Import_DEM.Import
{
    /// <summary>
    /// Handles the translation that moves projected coordinates near the world origin.
    /// Rhino loses accuracy when geometry sits far away from the origin, and coordinates such as
    /// UTM eastings are millions of units large.
    /// </summary>
    public static class OriginOffset
    {
        /// <summary>The document user text key that holds the offset of the first import.</summary>
        public const string DocumentUserTextKey = "Import_DEM.Offset";

        /// <summary>
        /// The key of the shapefile import plugin. A DEM and a shapefile of the same area then land
        /// in the same place.
        /// </summary>
        public const string ShapefileUserTextKey = "Import_SHP.Offset";

        /// <summary>The distance from the origin above which the plugin proposes an offset.</summary>
        public const double FarFromOriginDistance = 100000.0;

        private const double RoundingStep = 1000.0;

        /// <summary>Reads the offset that an earlier import wrote into the document.</summary>
        public static bool TryReadFromDocument(RhinoDoc doc, out Vector3d offset)
        {
            offset = Vector3d.Zero;
            if (doc is null)
                return false;

            return TryParse(doc.Strings.GetValue(DocumentUserTextKey), out offset)
                   || TryParse(doc.Strings.GetValue(ShapefileUserTextKey), out offset);
        }

        public static void WriteToDocument(RhinoDoc doc, Vector3d offset)
        {
            if (doc is null)
                return;

            var text = string.Format(
                CultureInfo.InvariantCulture,
                "{0:R},{1:R},{2:R}",
                offset.X,
                offset.Y,
                offset.Z);
            doc.Strings.SetString(DocumentUserTextKey, text);
        }

        public static bool TryParse(string? text, out Vector3d offset)
        {
            offset = Vector3d.Zero;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            var parts = text.Split(',');
            if (parts.Length != 3)
                return false;

            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y)
                || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                return false;

            offset = new Vector3d(x, y, z);
            return true;
        }

        /// <summary>True when the center of the raster lies further from the origin than the threshold.</summary>
        public static bool IsFarFromOrigin(double centerX, double centerY)
        {
            return Math.Abs(centerX) > FarFromOriginDistance || Math.Abs(centerY) > FarFromOriginDistance;
        }

        /// <summary>Proposes the translation that moves the center of the raster near the origin.</summary>
        public static Vector3d Suggest(double centerX, double centerY)
        {
            return new Vector3d(-RoundToStep(centerX), -RoundToStep(centerY), 0.0);
        }

        /// <summary>
        /// The center of the raster, from the GDAL geo transform. Element 0 and element 3 hold the
        /// upper left corner, element 1 and element 5 hold the cell size.
        /// </summary>
        public static (double X, double Y) CenterOf(Gdal.GdalInfo info)
        {
            var geoTransform = info.GeoTransform;
            return (
                geoTransform[0] + geoTransform[1] * info.Width / 2.0,
                geoTransform[3] + geoTransform[5] * info.Height / 2.0);
        }

        public static (double X, double Y) CenterOf(ElevationGrid grid) => (grid.CenterX, grid.CenterY);

        private static double RoundToStep(double value)
        {
            return Math.Round(value / RoundingStep, MidpointRounding.AwayFromZero) * RoundingStep;
        }
    }
}

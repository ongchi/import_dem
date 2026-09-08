using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Import_DEM.Gdal
{
    /// <summary>Runs "gdalinfo -json" and parses the result.</summary>
    public static class GdalInfoReader
    {
        /// <summary>Reads the metadata of one raster file.</summary>
        public static GdalInfo Read(GdalTools tools, string filePath)
        {
            var json = GdalProcess.Run(tools.GdalInfoPath, new[] { "-json", filePath });
            return Parse(json, filePath);
        }

        /// <summary>Parses the JSON that "gdalinfo -json" writes.</summary>
        /// <exception cref="GdalFailureException">The JSON is not a gdalinfo result.</exception>
        public static GdalInfo Parse(string json, string filePath)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException exception)
            {
                throw new GdalFailureException($"The gdalinfo output is not valid JSON: {exception.Message}");
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw new GdalFailureException("The gdalinfo output is not a JSON object.");

                var size = ReadSize(root);

                return new GdalInfo(
                    filePath,
                    ReadString(root, "driverShortName") ?? "unknown",
                    size.Width,
                    size.Height,
                    ReadGeoTransform(root),
                    ReadCoordinateSystemWkt(root),
                    ReadBands(root));
            }
        }

        private static (int Width, int Height) ReadSize(JsonElement root)
        {
            if (!root.TryGetProperty("size", out var size)
                || size.ValueKind != JsonValueKind.Array
                || size.GetArrayLength() != 2)
                throw new GdalFailureException("The gdalinfo output holds no raster size.");

            var width = size[0].GetInt32();
            var height = size[1].GetInt32();

            if (width < 2 || height < 2)
                throw new GdalFailureException(
                    $"The raster is {width} by {height} samples. A surface needs at least 2 samples in each direction.");

            return (width, height);
        }

        /// <summary>Reads the geo transform. A file with no georeference gets the GDAL default.</summary>
        private static IReadOnlyList<double> ReadGeoTransform(JsonElement root)
        {
            if (!root.TryGetProperty("geoTransform", out var element)
                || element.ValueKind != JsonValueKind.Array
                || element.GetArrayLength() != 6)
                return new[] { 0.0, 1.0, 0.0, 0.0, 0.0, -1.0 };

            var coefficients = new double[6];
            for (var index = 0; index < 6; index++)
                coefficients[index] = element[index].GetDouble();

            return coefficients;
        }

        private static string? ReadCoordinateSystemWkt(JsonElement root)
        {
            if (!root.TryGetProperty("coordinateSystem", out var coordinateSystem)
                || coordinateSystem.ValueKind != JsonValueKind.Object)
                return null;

            return ReadString(coordinateSystem, "wkt");
        }

        private static IReadOnlyList<GdalBandInfo> ReadBands(JsonElement root)
        {
            var bands = new List<GdalBandInfo>();

            if (root.TryGetProperty("bands", out var element) && element.ValueKind == JsonValueKind.Array)
            {
                foreach (var band in element.EnumerateArray())
                {
                    if (band.ValueKind != JsonValueKind.Object)
                        continue;

                    bands.Add(new GdalBandInfo(
                        band.TryGetProperty("band", out var index) ? index.GetInt32() : bands.Count + 1,
                        ReadString(band, "type") ?? "unknown",
                        ReadNoDataValue(band),
                        ReadString(band, "description")));
                }
            }

            if (bands.Count == 0)
                throw new GdalFailureException("The raster holds no band.");

            return bands;
        }

        /// <summary>
        /// Reads the no data value. GDAL writes a JSON number, but it writes the string "nan"
        /// when the value is not a number.
        /// </summary>
        private static double? ReadNoDataValue(JsonElement band)
        {
            if (!band.TryGetProperty("noDataValue", out var element))
                return null;

            return element.ValueKind switch
            {
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.String => double.TryParse(
                    element.GetString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed)
                    ? parsed
                    : double.NaN,
                _ => null,
            };
        }

        private static string? ReadString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }
    }
}

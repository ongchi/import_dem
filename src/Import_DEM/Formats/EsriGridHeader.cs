using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Import_DEM.Formats
{
    /// <summary>
    /// The text header of an ESRI .hdr labelled grid. The file holds one "NAME value" pair on each
    /// line. The plugin reads only the grids that it asks gdal_translate to write, so the reader
    /// accepts one band of 32 bit floating point samples in the BIL layout, and refuses the rest.
    /// </summary>
    public sealed class EsriGridHeader
    {
        private EsriGridHeader(
            int columns,
            int rows,
            bool isLittleEndian,
            double firstSampleX,
            double firstSampleY,
            double stepX,
            double stepY,
            double? noDataValue)
        {
            Columns = columns;
            Rows = rows;
            IsLittleEndian = isLittleEndian;
            FirstSampleX = firstSampleX;
            FirstSampleY = firstSampleY;
            StepX = stepX;
            StepY = stepY;
            NoDataValue = noDataValue;
        }

        public int Columns { get; }

        public int Rows { get; }

        public bool IsLittleEndian { get; }

        /// <summary>The X of the CENTER of the sample in the first column. This is the ULXMAP field.</summary>
        public double FirstSampleX { get; }

        /// <summary>The Y of the CENTER of the sample in the first row. This is the ULYMAP field.</summary>
        public double FirstSampleY { get; }

        public double StepX { get; }

        public double StepY { get; }

        public double? NoDataValue { get; }

        /// <summary>The size of the raw sample block in bytes.</summary>
        public long BlockByteCount => (long)Columns * Rows * sizeof(float);

        public static EsriGridHeader Read(string headerPath)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(headerPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new GridFormatException($"The grid header \"{headerPath}\" is not readable: {exception.Message}");
            }

            return Parse(lines);
        }

        public static EsriGridHeader Parse(IEnumerable<string> lines)
        {
            var fields = ReadFields(lines);

            RequireValue(fields, "NBANDS", "1", defaultValue: "1");
            RequireValue(fields, "LAYOUT", "BIL", defaultValue: "BIL");
            RequireValue(fields, "NBITS", "32", defaultValue: "8");
            RequireValue(fields, "PIXELTYPE", "FLOAT", defaultValue: "SIGNEDINT");

            var columns = ReadInteger(fields, "NCOLS");
            var rows = ReadInteger(fields, "NROWS");

            if (columns < 2 || rows < 2)
                throw new GridFormatException(
                    $"The grid is {columns} by {rows} samples. A surface needs at least 2 samples in each direction.");

            RequireRowBytes(fields, "BANDROWBYTES", columns);
            RequireRowBytes(fields, "TOTALROWBYTES", columns);

            if (ReadOptionalInteger(fields, "SKIPBYTES") is { } skipBytes && skipBytes != 0)
                throw new GridFormatException($"The grid header asks to skip {skipBytes} bytes, which the reader does not support.");

            // BYTEORDER I is Intel, which is little endian. M is Motorola, which is big endian.
            var byteOrder = ReadText(fields, "BYTEORDER", "I");
            var isLittleEndian = byteOrder.StartsWith("I", StringComparison.OrdinalIgnoreCase);

            return new EsriGridHeader(
                columns,
                rows,
                isLittleEndian,
                ReadDouble(fields, "ULXMAP", 0.0),
                ReadDouble(fields, "ULYMAP", 0.0),
                ReadDouble(fields, "XDIM", 1.0),
                ReadDouble(fields, "YDIM", 1.0),
                ReadOptionalDouble(fields, "NODATA"));
        }

        private static Dictionary<string, string> ReadFields(IEnumerable<string> lines)
        {
            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                var parts = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                    fields[parts[0].Trim()] = parts[1].Trim();
            }

            return fields;
        }

        private static void RequireValue(
            IReadOnlyDictionary<string, string> fields,
            string name,
            string expected,
            string defaultValue)
        {
            var value = fields.TryGetValue(name, out var text) ? text : defaultValue;
            if (!string.Equals(value, expected, StringComparison.OrdinalIgnoreCase))
                throw new GridFormatException($"The grid header reports {name} {value}, but the reader needs {expected}.");
        }

        /// <summary>Checks that a row holds the samples with no padding.</summary>
        private static void RequireRowBytes(IReadOnlyDictionary<string, string> fields, string name, int columns)
        {
            if (ReadOptionalInteger(fields, name) is not { } rowBytes)
                return;

            var expected = columns * sizeof(float);
            if (rowBytes != expected)
                throw new GridFormatException(
                    $"The grid header reports {name} {rowBytes}, but {columns} samples of 4 bytes need {expected}.");
        }

        private static int ReadInteger(IReadOnlyDictionary<string, string> fields, string name)
        {
            return ReadOptionalInteger(fields, name)
                   ?? throw new GridFormatException($"The grid header holds no {name} field.");
        }

        private static int? ReadOptionalInteger(IReadOnlyDictionary<string, string> fields, string name)
        {
            if (!fields.TryGetValue(name, out var text))
                return null;

            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                throw new GridFormatException($"The {name} field of the grid header is \"{text}\", which is not a whole number.");

            return value;
        }

        private static double ReadDouble(IReadOnlyDictionary<string, string> fields, string name, double defaultValue)
        {
            return ReadOptionalDouble(fields, name) ?? defaultValue;
        }

        private static double? ReadOptionalDouble(IReadOnlyDictionary<string, string> fields, string name)
        {
            if (!fields.TryGetValue(name, out var text))
                return null;

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                throw new GridFormatException($"The {name} field of the grid header is \"{text}\", which is not a number.");

            return value;
        }

        private static string ReadText(IReadOnlyDictionary<string, string> fields, string name, string defaultValue)
        {
            return fields.TryGetValue(name, out var text) && text.Length > 0 ? text : defaultValue;
        }
    }
}

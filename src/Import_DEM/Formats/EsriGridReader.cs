using System;
using System.Buffers.Binary;
using System.IO;
using Import_DEM.Grid;

namespace Import_DEM.Formats
{
    /// <summary>Reads an ESRI .hdr labelled grid into an elevation grid.</summary>
    public static class EsriGridReader
    {
        /// <summary>
        /// Reads the raw block beside its header. The header path is the grid path with the .hdr
        /// extension, which is where gdal_translate writes it.
        /// </summary>
        public static ElevationGrid Read(string gridPath)
        {
            var header = EsriGridHeader.Read(Path.ChangeExtension(gridPath, ".hdr"));
            var samples = ReadSamples(gridPath, header);

            return new ElevationGrid(
                samples,
                header.Columns,
                header.Rows,
                header.FirstSampleX,
                header.FirstSampleY,
                header.StepX,
                header.StepY,
                header.NoDataValue);
        }

        private static double[] ReadSamples(string gridPath, EsriGridHeader header)
        {
            byte[] block;
            try
            {
                block = File.ReadAllBytes(gridPath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new GridFormatException($"The grid \"{gridPath}\" is not readable: {exception.Message}");
            }

            if (block.LongLength < header.BlockByteCount)
                throw new GridFormatException(
                    $"The grid holds {block.LongLength} bytes, but {header.Columns} by {header.Rows} samples need {header.BlockByteCount}.");

            var sampleCount = header.Columns * header.Rows;
            var samples = new double[sampleCount];

            for (var index = 0; index < sampleCount; index++)
            {
                var bytes = block.AsSpan(index * sizeof(float), sizeof(float));
                samples[index] = header.IsLittleEndian
                    ? BinaryPrimitives.ReadSingleLittleEndian(bytes)
                    : BinaryPrimitives.ReadSingleBigEndian(bytes);
            }

            return samples;
        }
    }
}

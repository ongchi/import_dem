using System;
using Import_DEM.Formats;
using Import_DEM.Gdal;
using Import_DEM.Grid;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Import_DEM.Import
{
    /// <summary>Runs one import: the conversion, the grid, the tiles and the document objects.</summary>
    public sealed class ElevationImporter
    {
        /// <summary>The layer user text key that holds the coordinate system of the source file.</summary>
        public const string ProjectionUserTextKey = "Import_DEM.Projection";

        /// <summary>Reads the raster and adds the surfaces to the document.</summary>
        public ImportReport Import(RhinoDoc doc, ImageSummary summary, ImportOptions options)
        {
            var report = new ImportReport { Stride = options.Stride };
            RequireAxisAlignedRaster(summary.Info);
            WarnAboutTheBand(summary.Info, options, report);

            var grid = ReadGrid(summary, options);
            report.Columns = grid.Columns;
            report.Rows = grid.Rows;

            var offset = options.ApplyOffset ? options.Offset : Vector3d.Zero;
            HandleVoids(grid, options, report);
            AddSurfaces(doc, grid, options, offset, summary.Info.CoordinateSystemWkt, report);

            if (options.ApplyOffset)
                OriginOffset.WriteToDocument(doc, offset);

            return report;
        }

        private static void RequireAxisAlignedRaster(GdalInfo info)
        {
            if (info.IsAxisAligned)
                return;

            throw new GdalFailureException(
                "The raster is rotated or sheared, so its samples do not sit on a grid that follows "
                + "the X and the Y axis. A NURBS surface needs such a grid. Use gdalwarp to write a "
                + "north up copy of the file, then import the copy.");
        }

        private static void WarnAboutTheBand(GdalInfo info, ImportOptions options, ImportReport report)
        {
            var band = info.FindBand(options.Band);
            if (band is null)
            {
                throw new GdalFailureException(
                    $"The file holds {info.Bands.Count} bands, so band {options.Band} does not exist.");
            }

            if (band.NeedsMoreThanFloat32)
            {
                report.AddWarning(
                    "The band holds 64 bit elevations. The import passes them through a 32 bit "
                    + "intermediate, which keeps about 7 digits.");
            }
        }

        /// <summary>Converts the band with GDAL and reads the temporary grid.</summary>
        private static ElevationGrid ReadGrid(ImageSummary summary, ImportOptions options)
        {
            var columns = GridStride.SampledLength(summary.Info.Width, options.Stride);
            var rows = GridStride.SampledLength(summary.Info.Height, options.Stride);

            var translator = new GdalTranslator(summary.Tools);
            using var conversion = translator.Convert(
                summary.FilePath,
                options.Band,
                columns,
                rows,
                options.Resampling);

            return EsriGridReader.Read(conversion.GridPath);
        }

        private static void HandleVoids(ElevationGrid grid, ImportOptions options, ImportReport report)
        {
            report.VoidCount = options.NoDataMode switch
            {
                NoDataMode.Fill => NoDataFiller.Fill(grid),
                NoDataMode.Constant => NoDataFiller.FillWithConstant(grid, options.NoDataElevation),
                _ => grid.CountVoids(),
            };

            if (report.VoidCount > 0 && options.NoDataMode == NoDataMode.Fill)
                report.AddWarning($"{report.VoidCount} samples held no elevation and took the mean of their neighbours.");
        }

        private static void AddSurfaces(
            RhinoDoc doc,
            ElevationGrid grid,
            ImportOptions options,
            Vector3d offset,
            string? projectionWkt,
            ImportReport report)
        {
            var tiles = GridTiler.Split(grid.Columns, grid.Rows, options.MaxPatchSize);
            report.TileCount = tiles.Count;

            var layerIndex = EnsureLayer(doc, options.LayerName);
            StoreProjection(doc, layerIndex, projectionWkt);

            var attributes = new ObjectAttributes { LayerIndex = layerIndex };
            if (options.GroupTiles && tiles.Count > 1)
                attributes.AddToGroup(doc.Groups.Add(options.LayerName));

            foreach (var tile in tiles)
            {
                if (options.NoDataMode == NoDataMode.SkipTile && SurfaceBuilder.HoldsVoid(grid, tile))
                {
                    report.SkippedTileCount++;
                    continue;
                }

                var surface = SurfaceBuilder.Build(grid, tile, options.SurfaceType, offset, options.ElevationFactor);
                if (surface is null)
                {
                    report.FailedTileCount++;
                    continue;
                }

                if (doc.Objects.AddSurface(surface, attributes) == Guid.Empty)
                    report.FailedTileCount++;
                else
                    report.SurfaceCount++;
            }

            if (report.FailedTileCount > 0)
                report.AddWarning($"Rhino could not build {report.FailedTileCount} of the {tiles.Count} surfaces.");

            if (report.SurfaceCount == 0)
                report.AddWarning("The import added no surface.");
        }

        /// <summary>Finds the layer, or adds it.</summary>
        private static int EnsureLayer(RhinoDoc doc, string layerName)
        {
            var name = string.IsNullOrWhiteSpace(layerName) ? "DEM" : layerName.Trim();

            var index = doc.Layers.FindByFullPath(name, -1);
            if (index < 0)
                index = doc.Layers.Add(name, System.Drawing.Color.Black);

            return index < 0 ? doc.Layers.CurrentLayerIndex : index;
        }

        /// <summary>Stores the coordinate system text of the source on the import layer.</summary>
        private static void StoreProjection(RhinoDoc doc, int layerIndex, string? wkt)
        {
            if (string.IsNullOrWhiteSpace(wkt) || layerIndex < 0 || layerIndex >= doc.Layers.Count)
                return;

            doc.Layers[layerIndex].SetUserString(ProjectionUserTextKey, wkt);
        }
    }
}

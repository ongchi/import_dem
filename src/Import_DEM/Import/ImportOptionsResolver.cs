using Import_DEM.Grid;
using Import_DEM.UI;
using Rhino;

namespace Import_DEM.Import
{
    /// <summary>Builds the import options: first the defaults, then the user changes.</summary>
    public static class ImportOptionsResolver
    {
        /// <summary>
        /// Returns the options for one import, or null when the user cancels.
        /// The interactive mode shows the options dialog. The other modes use the defaults.
        /// </summary>
        public static ImportOptions? Resolve(RhinoDoc doc, ImageSummary summary, bool interactive)
        {
            var options = CreateDefaults(doc, summary);

            if (!interactive)
                return options;

            return ImportOptionsDialog.Show(doc, summary, options) ? options : null;
        }

        /// <summary>
        /// Builds the default options. The offset of an earlier import in the same document wins,
        /// so that every file lands in the same place.
        /// </summary>
        public static ImportOptions CreateDefaults(RhinoDoc doc, ImageSummary summary)
        {
            var options = new ImportOptions
            {
                LayerName = summary.DefaultLayerName(),
                SurfaceType = SurfaceType.Interpolated,
                Band = summary.Info.Bands[0].Index,
            };

            options.MaxPatchSize = ImportOptions.DefaultPatchSize(options.SurfaceType);
            options.Stride = GridStride.Suggest(summary.Info.Width, summary.Info.Height, options.SampleBudget);

            var center = OriginOffset.CenterOf(summary.Info);

            if (OriginOffset.TryReadFromDocument(doc, out var documentOffset))
            {
                options.ApplyOffset = true;
                options.Offset = documentOffset;
            }
            else if (OriginOffset.IsFarFromOrigin(center.X, center.Y))
            {
                options.ApplyOffset = true;
                options.Offset = OriginOffset.Suggest(center.X, center.Y);
            }

            return options;
        }

        /// <summary>
        /// Applies the stride and the tile size that suit a surface type. The dialog calls this
        /// when the user changes the type, so the counts stay sensible.
        /// </summary>
        public static void ApplySurfaceTypeDefaults(ImageSummary summary, ImportOptions options)
        {
            options.MaxPatchSize = ImportOptions.DefaultPatchSize(options.SurfaceType);
            options.Stride = GridStride.Suggest(summary.Info.Width, summary.Info.Height, options.SampleBudget);
        }
    }
}

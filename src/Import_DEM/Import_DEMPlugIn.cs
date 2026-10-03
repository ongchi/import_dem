using System;
using System.IO;
using Import_DEM.Formats;
using Import_DEM.Gdal;
using Import_DEM.Import;
using Rhino;
using Rhino.FileIO;
using Rhino.PlugIns;

namespace Import_DEM
{
    /// <summary>Adds the Digital Elevation Model formats to the Rhino import dialog.</summary>
    public sealed class Import_DEMPlugIn : FileImportPlugIn
    {
        public Import_DEMPlugIn()
        {
            Instance = this;
        }

        public static Import_DEMPlugIn? Instance { get; private set; }

        protected override FileTypeList AddFileTypes(FileReadOptions options)
        {
            var fileTypes = new FileTypeList();

            foreach (var format in SupportedFormats.All)
                fileTypes.AddFileType($"{format.Description} (*.{format.Extension})", format.Extension);

            return fileTypes;
        }

        protected override bool ReadFile(string filename, int index, RhinoDoc doc, FileReadOptions options)
        {
            try
            {
                using var summary = ImageSummary.Read(filename, GdalLocation.Folder);

                var importOptions = ImportOptionsResolver.Resolve(doc, summary, interactive: !options.BatchMode);
                if (importOptions is null)
                    return false;

                var importer = new ElevationImporter();
                var report = importer.Import(doc, summary, importOptions);
                ReportWriter.Write(report, filename);
                doc.Views.Redraw();
                return true;
            }
            catch (Exception exception) when (exception is GdalNotFoundException or GdalFailureException
                                                  or GridFormatException or IOException or UnauthorizedAccessException)
            {
                RhinoApp.WriteLine($"DEM import failed: {exception.Message}");
                return false;
            }
        }
    }
}

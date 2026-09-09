using System;
using System.IO;
using Import_DEM.Formats;
using Import_DEM.Gdal;
using Import_DEM.Import;
using Import_DEM.UI;
using Rhino;
using Rhino.Commands;
using Rhino.Input;

namespace Import_DEM
{
    /// <summary>Imports one DEM file. The command works in interactive mode and in script mode.</summary>
    public sealed class ImportDemCommand : Command
    {
        public override string EnglishName => "ImportDEM";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            var interactive = mode == RunMode.Interactive;

            var filePath = GetFilePath(interactive);
            if (filePath is null)
                return Result.Cancel;

            if (!File.Exists(filePath))
            {
                RhinoApp.WriteLine($"The file \"{filePath}\" does not exist.");
                return Result.Failure;
            }

            try
            {
                var summary = ReadSummary(filePath, interactive);
                if (summary is null)
                    return Result.Cancel;

                var options = ImportOptionsResolver.CreateDefaults(doc, summary);

                var accepted = interactive
                    ? ImportOptionsDialog.Show(doc, summary, options)
                    : ImportOptionsPrompt.TryPrompt(doc, summary, options);
                if (!accepted)
                    return Result.Cancel;

                var importer = new ElevationImporter();
                var report = importer.Import(doc, summary, options);
                ReportWriter.Write(report, filePath);

                doc.Views.Redraw();
                return Result.Success;
            }
            catch (Exception exception) when (exception is GdalNotFoundException or GdalFailureException
                                                  or GridFormatException or IOException or UnauthorizedAccessException)
            {
                RhinoApp.WriteLine($"DEM import failed: {exception.Message}");
                return Result.Failure;
            }
        }

        /// <summary>
        /// Reads the file metadata. When GDAL is missing and the user is present, the command asks
        /// for the GDAL folder one time and tries again. Returns null when the user cancels.
        /// </summary>
        private static ImageSummary? ReadSummary(string filePath, bool interactive)
        {
            try
            {
                return ImageSummary.Read(filePath, GdalLocation.Folder);
            }
            catch (GdalNotFoundException exception) when (interactive)
            {
                RhinoApp.WriteLine(exception.Message);

                if (!GdalLocation.TryAskForFolder())
                    return null;

                return ImageSummary.Read(filePath, GdalLocation.Folder);
            }
        }

        private static string? GetFilePath(bool interactive)
        {
            if (!interactive)
            {
                var scriptPath = string.Empty;
                return RhinoGet.GetString("DEM file path", false, ref scriptPath) == Result.Success
                    ? scriptPath.Trim().Trim('"')
                    : null;
            }

            var dialog = new Rhino.UI.OpenFileDialog
            {
                Title = "Import DEM",
                Filter = SupportedFormats.OpenDialogFilter,
                MultiSelect = false,
            };

            return dialog.ShowOpenDialog() ? dialog.FileName : null;
        }
    }
}

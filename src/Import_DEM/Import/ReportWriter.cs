using System.IO;
using Rhino;

namespace Import_DEM.Import
{
    /// <summary>Writes the result of an import to the Rhino command line.</summary>
    public static class ReportWriter
    {
        public static void Write(ImportReport report, string filePath)
        {
            RhinoApp.WriteLine(report.ToSummary(Path.GetFileName(filePath)));

            foreach (var warning in report.Warnings)
                RhinoApp.WriteLine($"  Warning: {warning}");
        }
    }
}

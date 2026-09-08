using System.IO;

namespace Import_DEM.Tests
{
    /// <summary>Finds the fixtures that tools/make_fixtures.py writes.</summary>
    internal static class Fixtures
    {
        public static string Path(string fileName)
        {
            return System.IO.Path.Combine(Directory.GetCurrentDirectory(), "fixtures", fileName);
        }

        public static string ReadText(string fileName)
        {
            return File.ReadAllText(Path(fileName));
        }
    }
}

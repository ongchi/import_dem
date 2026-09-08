using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Import_DEM.Gdal
{
    /// <summary>Runs one GDAL tool and collects its output.</summary>
    internal static class GdalProcess
    {
        /// <summary>The time that one GDAL run gets. A large DEM conversion is slow.</summary>
        public const int DefaultTimeoutMilliseconds = 10 * 60 * 1000;

        /// <summary>
        /// Runs the tool and returns the standard output. The arguments go to the process one by
        /// one, so a path with a space needs no quotation mark.
        /// </summary>
        /// <exception cref="GdalFailureException">The tool did not start, timed out, or stopped with an error.</exception>
        public static string Run(
            string executablePath,
            IReadOnlyList<string> arguments,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using var process = Start(startInfo, executablePath);

            // Both streams are read while the process runs. A full pipe buffer would block the tool.
            var standardOutput = new StringBuilder();
            var standardError = new StringBuilder();
            process.OutputDataReceived += (_, eventArgs) => Append(standardOutput, eventArgs.Data);
            process.ErrorDataReceived += (_, eventArgs) => Append(standardError, eventArgs.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(timeoutMilliseconds))
            {
                TryKill(process);
                throw new GdalFailureException(
                    $"\"{Path.GetFileName(executablePath)}\" did not finish in {timeoutMilliseconds / 1000} seconds.");
            }

            // WaitForExit() with no argument flushes the two output readers.
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var message = standardError.ToString().Trim();
                if (message.Length == 0)
                    message = $"the tool stopped with the code {process.ExitCode}";

                throw new GdalFailureException($"\"{Path.GetFileName(executablePath)}\" failed: {message}");
            }

            return standardOutput.ToString();
        }

        private static Process Start(ProcessStartInfo startInfo, string executablePath)
        {
            try
            {
                return Process.Start(startInfo)
                       ?? throw new GdalFailureException($"\"{executablePath}\" did not start.");
            }
            catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                throw new GdalFailureException($"\"{executablePath}\" did not start: {exception.Message}");
            }
        }

        private static void Append(StringBuilder text, string? line)
        {
            if (line is not null)
                text.AppendLine(line);
        }

        private static void TryKill(Process process)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or System.ComponentModel.Win32Exception)
            {
                // The process ended by itself between the timeout and the kill. Nothing to do.
            }
        }
    }
}

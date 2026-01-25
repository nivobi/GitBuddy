using System.Diagnostics;

namespace GitBuddy.Infrastructure
{
    public class ProcessRunner : IProcessRunner
    {
        public string Run(string fileName, string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);

            if (process == null)
            {
                return "Error: Could not start process";
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (string.IsNullOrWhiteSpace(output))
            {
                return error.Trim();
            }

            return output.Trim();
        }
    }
}

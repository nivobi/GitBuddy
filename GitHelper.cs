using System.Diagnostics;

namespace GitBuddy
{
    public static class GitHelper
    {
        public static string Run(string args, string fileName = "git")
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = fileName, 
                Arguments = args,
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
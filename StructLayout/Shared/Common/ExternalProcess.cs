using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace StructLayout
{
    public class ExternalProcess
    {
        public string Log { set; get; } = null;

        public int ExecuteSync(string toolPath, string arguments)
        {
            ClearLog();
            return RunProcess(toolPath, arguments);
        }

        public Task<int> ExecuteAsync(string toolPath, string arguments)
        {
            ClearLog();
            return Task.Run(() => RunProcess(toolPath, arguments));
        }

        private int RunProcess(string toolPath, string arguments)
        {
            Process process = new Process();

            process.StartInfo.FileName = toolPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            process.ErrorDataReceived += (sender, errorLine) =>
            {
                if (errorLine.Data != null)
                {
                    OutputLine(errorLine.Data);
                }
            };
            process.OutputDataReceived += (sender, outputLine) =>
            {
                if (outputLine.Data != null)
                {
                    OutputLine(outputLine.Data);
                }
            };

            try
            {
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                process.WaitForExit();
            }
            catch (Exception error)
            {
                OutputLine(error.Message);
                return -1;
            }

            int exitCode = process.ExitCode;
            process.Close();

            return exitCode;
        }

        private void OutputLine(string str)
        {
            if (str != null)
            {
                AppendToLog(str);
#pragma warning disable 414, VSTHRD010
                OutputLog.GetPane().OutputStringThreadSafe(str + '\n');
#pragma warning restore VSTHRD010
            }
        }

        private void ClearLog()
        {
            Log = Log == null ? null : "";
        }

        private void AppendToLog(string str)
        {
            if (Log != null)
            {
                Log += str + '\n';
            }
        }
    }
}
 

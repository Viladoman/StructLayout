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
            var process = StartProcess(toolPath, arguments);

            if (process == null)
            {
                return -1;
            }

            WaitForExit(process);

            return process.ExitCode;
        }

        public async Task<int> ExecuteAsync(string toolPath, string arguments)
        {
            ClearLog();
            var process = StartProcess(toolPath, arguments);

            if (process == null)
            {
                return -1;
            }

            await Task.Run(() => WaitForExit(process));

            return process.ExitCode;
        }

        private Process StartProcess(string toolPath, string arguments)
        {
            var process = new Process();

            process.StartInfo.FileName = toolPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

            try
            {
                process.Start();
            }
            catch (Exception error)
            {
                OutputLine(error.Message);
                return null;
            }

            return process;
        }

        private void WaitForExit(Process process)
        {
            //Handle output
            while (!process.StandardOutput.EndOfStream || !process.StandardError.EndOfStream)
            {
                if (!process.StandardOutput.EndOfStream)
                {
                    OutputLine(process.StandardOutput.ReadLine());
                }

                if (!process.StandardError.EndOfStream)
                {
                    OutputLine(process.StandardError.ReadLine());
                }
            }

            process.WaitForExit();
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
 

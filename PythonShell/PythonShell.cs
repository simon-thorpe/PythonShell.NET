using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using PythonShell.Exceptions;
using PythonShell.Data;

namespace PythonShell
{
    namespace Data
    {
        internal class PathResolver
        {
            public static string GetExecutableFilePath(string fileName)
            {
                ProcessStartInfo psi = new ProcessStartInfo(@"C:\Windows\System32\where.exe", fileName);
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process p = new Process();
                p.StartInfo = psi;
                p.Start();
                string stdout = p.StandardOutput.ReadToEnd().TrimEnd();
                p.WaitForExit();
                p.Close();
                return stdout == "" ? null : stdout;
            }
            public static string GetPythonExecutableFilePath(PythonVersion pythonVersion)
            {
                switch (pythonVersion)
                {
                    case PythonVersion.PythonW:
                        return GetExecutableFilePath("pythonw.exe"); // TODO: Should this be cached?
                    case PythonVersion.Python:
                        return GetExecutableFilePath("python.exe"); // TODO: Should this be cached?
                    case PythonVersion.Pythonw27:
                        return @"C:\Python27\pythonw.exe";
                    case PythonVersion.Python27:
                        return @"C:\Python27\python.exe";
                    case PythonVersion.Pythonw32:
                        return @"C:\Python32\pythonw.exe";
                    case PythonVersion.Pythonw34:
                        return @"C:\Python34\pythonw.exe";
                    case PythonVersion.Python32:
                        return @"C:\Python32\python.exe";
                    case PythonVersion.Python34:
                        return @"C:\Python34\python.exe";
                    default:
                        throw new NotSupportedException("PythonVersion of '" + pythonVersion.ToString() + "' is not supported yet.");
                }
            }
        }
        public enum PythonVersion
        {
            /// <summary>
            /// Default executable pythonw.exe gets searched in %PATH%
            /// </summary>
            PythonW,
            /// <summary>
            /// Default executable path: C:\Python27\pythonw.exe
            /// </summary>
            Pythonw27,
            /// <summary>
            /// Default executable path: C:\Python32\pythonw.exe
            /// </summary>
            Pythonw32,
            /// <summary>
            /// Default executable path: C:\Python34\pythonw.exe
            /// </summary>
            Pythonw34,
            /// <summary>
            /// Default executable python.exe gets searched in %PATH%
            /// </summary>
            Python,
            /// <summary>
            /// Default executable path: C:\Python27\python.exe
            /// Remember stdout is redirected so don't expect to watch output in console while running.
            /// </summary>
            Python27,
            /// <summary>
            /// Default executable path: C:\Python32\python.exe
            /// Remember stdout is redirected so don't expect to watch output in console while running.
            /// </summary>
            Python32,
            /// <summary>
            /// Default executable path: C:\Python34\python.exe
            /// Remember stdout is redirected so don't expect to watch output in console while running.
            /// </summary>
            Python34,
        }
    }
    /// <summary>
    /// Eval python scripts using the system's python install and return the output.
    /// </summary>
    public class PythonShell
    {
        public PythonShell(PythonVersion pythonVersion = PythonVersion.PythonW)
        {
            this.PythonVersion = pythonVersion;
        }
        /// <summary>
        /// Overrides the PythonVersion property. If this property is left null the executable path will be detected from the PythonVersion property.
        /// </summary>
        public string PythonExecutableFilePath = null;
        /// <summary>
        /// The working directory passed to ProcessStartInfo.WorkingDirectory.
        /// </summary>
        public string WorkingDirectory;
        public PythonVersion PythonVersion = PythonVersion.PythonW;
        private StringBuilder StandardOutput = null;
        private StringBuilder StandardError = null;

        private string _eval(string script = null, string orFilePath = null, string stdinString = null)
        {
            StandardOutput = new StringBuilder();
            StandardError = new StringBuilder();
            string path = this.PythonExecutableFilePath == null ? PathResolver.GetPythonExecutableFilePath(PythonVersion) : this.PythonExecutableFilePath;
            ProcessStartInfo psi = new ProcessStartInfo(path);
            if (orFilePath != null) // Run .py script file.
                psi.Arguments = "\"" + orFilePath.Replace("\"", "\"\"") + "\"";
            else // Run as script from arg.
                psi.Arguments = "-c \"" + script.Replace("\"", "\"\"") + "\"";
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            if (stdinString != null)
                psi.RedirectStandardInput = true;
            psi.UseShellExecute = false;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            if (WorkingDirectory != null)
                psi.WorkingDirectory = WorkingDirectory;

            Process p = new Process();
            p.OutputDataReceived += new DataReceivedEventHandler(p_OutputDataReceived);
            p.ErrorDataReceived += new DataReceivedEventHandler(p_ErrorDataReceived);
            p.StartInfo = psi;
            try
            {
                p.Start();
            }
            catch (System.ComponentModel.Win32Exception e)
            {
                if (e.Message == "The system cannot find the file specified")
                {
                    throw new PythonNotInstalledException(message: "Python is not installed on the system. Cannot execute " + path, expectedPythonExecutable: path, interpreter: this);
                }
            }
            if (stdinString != null)
            {
                p.StandardInput.Write(stdinString);
                p.StandardInput.Close();
            }
            p.BeginOutputReadLine();
            p.BeginErrorReadLine();
            p.WaitForExit();
            string stdout = this.StandardOutput.ToString();
            string stderr = this.StandardError.ToString();
            p.Close();
            if (!string.IsNullOrEmpty(stderr))
                throw new PythonException(stdout, stderr, script, path, this);
            return stdout;
        }
        /// <summary>
        /// Executes python script content and returns stdout.
        /// </summary>
        /// <param name="script"></param>
        /// <param name="stdinString"></param>
        /// <returns></returns>
        public string Eval(string script, string stdinString = null)
        {
            return _eval(script: script, stdinString: stdinString);
        }
        /// <summary>
        /// Executes a .py file and returns stdout.
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="stdinString"></param>
        /// <returns></returns>
        public string EvalFile(string filePath, string stdinString = null)
        {
            return _eval(orFilePath: filePath, stdinString: stdinString);
        }
        /// <summary>
        /// Executes python script content and returns stdout.
        /// The content is written to a temporary file and executes as a .py script then deleted. This is the preferred method for large scripts.
        /// </summary>
        /// <param name="script"></param>
        /// <param name="stdinString"></param>
        /// <param name="tempFilePath">A random file in the system temp folder will be used if left null.</param>
        /// <returns></returns>
        public string EvalAsFile(string script, string stdinString = null, string tempFilePath = null)
        {
            if (tempFilePath == null)
                tempFilePath = Environment.GetEnvironmentVariable("temp") + @"\" + Guid.NewGuid() + ".py";
            File.WriteAllText(tempFilePath, script);
            try
            {
                return _eval(orFilePath: tempFilePath, stdinString: stdinString);
            }
            finally
            {
                File.Delete(tempFilePath);
            }
        }
        void p_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;

            StandardOutput.AppendLine(e.Data);
        }
        void p_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data == null)
                return;

            StandardError.AppendLine(e.Data);
        }
    }
    namespace Exceptions
    {
        public class PythonException : ApplicationException
        {
            public PythonException(string message)
                : base(message)
            {
            }

            // Useful for debugging.
            public string StandardOutput { get; internal set; }
            public string StandardError { get; internal set; }
            public string Script { get; internal set; }
            public string PythonExecutablePath { get; internal set; }
            public PythonVersion PythonVersion { get; internal set; }
            public PythonShell Interpreter { get; internal set; }
            public string ErrorType
            {
                get
                {
                    if (string.IsNullOrWhiteSpace(this.StandardError))
                        return null;

                    string lastErrorLine = this.StandardError.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Last();
                    return lastErrorLine.Substring(0, lastErrorLine.IndexOf(':'));
                }
            }
            public string ErrorMessage
            {
                get { return GetPythonErrorMessage(stderr: this.StandardError); }
            }
            private static string GetPythonErrorMessage(string stderr, string defaultIfErrorMessageEmpty = null)
            {
                if (string.IsNullOrWhiteSpace(stderr))
                    return null;

                string lastErrorLine = stderr.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Last();
                int i = lastErrorLine.IndexOf(':') + 1;
                string errorMsg = lastErrorLine.Substring(i, lastErrorLine.Length - i).Trim();

                if (defaultIfErrorMessageEmpty != null && string.IsNullOrWhiteSpace(errorMsg))
                    return defaultIfErrorMessageEmpty;
                else
                    return errorMsg;
            }

            public PythonException(string standardOutput, string standardError, string script, string pythonExecutablePath, PythonShell interpreter)
                : base(GetPythonErrorMessage(stderr: standardError, defaultIfErrorMessageEmpty: standardError))
            {
                this.StandardOutput = standardOutput;
                this.StandardError = standardError;
                this.Script = script;
                this.PythonExecutablePath = pythonExecutablePath;
                this.Interpreter = interpreter;
            }

            public PythonException(PythonException pythonException)
                : base(pythonException.Message)
            {
                this.StandardOutput = pythonException.StandardOutput;
                this.StandardError = pythonException.StandardError;
                this.Script = pythonException.Script;
                this.PythonExecutablePath = pythonException.PythonExecutablePath;
                this.Interpreter = pythonException.Interpreter;
            }
        }
        public class PythonNotInstalledException : ApplicationException
        {
            public PythonShell Interpreter { get; internal set; }
            public string ExpectedPythonExecutable { get; internal set; }
            public PythonNotInstalledException(string message, string expectedPythonExecutable, PythonShell interpreter)
                : base(message)
            {
                this.ExpectedPythonExecutable = expectedPythonExecutable;
                this.Interpreter = interpreter;
            }
        }
    }
}

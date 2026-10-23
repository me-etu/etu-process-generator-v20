using System;
using System.IO;
using System.Text;

namespace TIA_LIB.Generation
{
    // Covers the complete synchronous app execution, including selection, Portal
    // connection, model construction, import/compile and HMI generation failures.
    public static class GenerationExecutionLog
    {
        const int ConsoleLimit = 40000;

        public static int Run(string application, Action generate, Func<string> describeSettings = null, bool pauseOnFailure = false)
        {
            if (generate == null) throw new ArgumentNullException(nameof(generate));
            var started = DateTime.UtcNow;
            string workingDirectory = Directory.GetCurrentDirectory();
            var output = Console.Out; var errors = Console.Error;
            var tail = new ConsoleTail();
            try
            {
                Console.SetOut(new RecordingWriter(output, tail));
                Console.SetError(new RecordingWriter(errors, tail));
                generate();
                return 0;
            }
            catch (Exception failure)
            {
                string settings = "Unavailable";
                try { if (describeSettings != null) settings = describeSettings(); }
                catch (Exception contextError) { settings = "Unavailable: " + contextError.Message; }
                var report = new StringBuilder();
                report.AppendLine("ETU generator execution failure");
                report.AppendLine("Application: " + application);
                report.AppendLine("Library version: " + typeof(GenerationExecutionLog).Assembly.GetName().Version);
                report.AppendLine("Started UTC: " + started.ToString("O"));
                report.AppendLine("Failed UTC: " + DateTime.UtcNow.ToString("O"));
                report.AppendLine("Working directory: " + workingDirectory);
                report.AppendLine("Executable directory: " + AppDomain.CurrentDomain.BaseDirectory);
                report.AppendLine("Settings at failure: " + settings);
                report.AppendLine();
                report.AppendLine("Recent console output (last " + ConsoleLimit + " characters):");
                report.AppendLine(tail.Snapshot());
                report.AppendLine();
                report.AppendLine("Full exception (including inner exceptions and stack trace):");
                report.AppendLine(failure.ToString());

                string path = null;
                var saveErrors = new StringBuilder();
                for (int location = 0; location < 2; location++)
                {
                    string directory = location == 0 ? "GenerationErrors" : "temporary error-log directory";
                    try
                    {
                        directory = location == 0 ? Path.Combine(workingDirectory, "GenerationErrors") : Path.Combine(Path.GetTempPath(), "EtuGeneratorErrors");
                        Directory.CreateDirectory(directory);
                        string candidate = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".log");
                        using (var file = new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
                        using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
                        {
                            writer.Write(report.ToString());
                            if (saveErrors.Length != 0) writer.WriteLine("\nPrimary log location unavailable:\n" + saveErrors);
                        }
                        path = candidate;
                        break;
                    }
                    catch (Exception saveError) { saveErrors.AppendLine(directory + ": " + saveError.Message); }
                }
                try
                {
                    errors.WriteLine(failure.ToString());
                    errors.WriteLine(path == null ? "Could not save an error log. " + saveErrors : "Generation failed. Error log: " + path);
                    errors.Flush();
                    if (pauseOnFailure && !Console.IsInputRedirected)
                    {
                        errors.WriteLine("Press any key to exit..."); errors.Flush();
                        Console.ReadKey(true);
                    }
                }
                catch (Exception) { /* A broken console must not replace the original failure. */ }
                return 1;
            }
            finally
            {
                Console.SetOut(output);
                Console.SetError(errors);
            }
        }

        sealed class ConsoleTail
        {
            readonly StringBuilder text = new StringBuilder();
            readonly object gate = new object();
            public void Append(string value)
            {
                if (string.IsNullOrEmpty(value)) return;
                lock (gate)
                {
                    if (value.Length >= ConsoleLimit) { text.Clear(); text.Append(value, value.Length - ConsoleLimit, ConsoleLimit); }
                    else
                    {
                        int overflow = text.Length + value.Length - ConsoleLimit;
                        if (overflow > 0) text.Remove(0, overflow);
                        text.Append(value);
                    }
                }
            }
            public string Snapshot() { lock (gate) return text.ToString(); }
        }
        sealed class RecordingWriter : TextWriter
        {
            readonly TextWriter destination;
            readonly ConsoleTail tail;
            public RecordingWriter(TextWriter destination, ConsoleTail tail) { this.destination = destination; this.tail = tail; }
            public override Encoding Encoding { get { return destination.Encoding; } }
            public override void Write(char value) { tail.Append(value.ToString()); destination.Write(value); }
            public override void Write(string value) { tail.Append(value); destination.Write(value); }
            public override void Write(char[] buffer, int index, int count) { Write(new string(buffer, index, count)); }
            public override void WriteLine(string value) { Write(value); Write(destination.NewLine); }
            public override void Flush() { destination.Flush(); }
        }
    }
}

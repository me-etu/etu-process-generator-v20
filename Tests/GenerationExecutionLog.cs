using System;
using System.IO;
using TIA_LIB.Generation;

public static class GenerationExecutionLogTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Fail()
    {
        try { throw new InvalidOperationException("Inner Siemens-style import failure"); }
        catch (Exception inner) { throw new ApplicationException("Outer generation failure", inner); }
    }
    public static void Run()
    {
        string previous = Directory.GetCurrentDirectory();
        var originalOut = Console.Out; var originalError = Console.Error;
        string scratch = Path.Combine(Path.GetTempPath(), "ETU-log-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);
        try
        {
            Directory.SetCurrentDirectory(scratch);
            var output = new StringWriter(); var error = new StringWriter();
            Console.SetOut(output); Console.SetError(error);
            var beforeOut = Console.Out; var beforeError = Console.Error;
            Check(GenerationExecutionLog.Run("Test V21", () => Console.WriteLine("Successful execution")) == 0, "Success exit status differs.");
            Check(!Directory.Exists("GenerationErrors"), "Successful run created an unnecessary error folder.");
            Check(output.ToString().Contains("Successful execution"), "Console output was swallowed.");
            Check(ReferenceEquals(beforeOut, Console.Out) && ReferenceEquals(beforeError, Console.Error), "Console streams were not restored after success.");

            int result = GenerationExecutionLog.Run("Test V21", () => {
                Console.Write("Old output to discard");
                Console.Write(new string('x', 41000));
                Console.WriteLine("Prepared UDT: hwIN_TK003; unit °C");
                Console.Error.WriteLine("Compiler diagnostic before failure");
                Fail();
            }, () => "Language=Scl; Staging=GeneratedDbUdt");
            Check(result == 1, "Failure did not return nonzero exit status.");
            var logs = Directory.GetFiles("GenerationErrors", "*.log");
            Check(logs.Length == 1, "Failed execution should create exactly one report.");
            string report = File.ReadAllText(logs[0]);
            foreach (string expected in new[] { "Test V21", "Started UTC:", "Failed UTC:", scratch, "Language=Scl; Staging=GeneratedDbUdt", "Prepared UDT: hwIN_TK003; unit °C",
                "Compiler diagnostic before failure", "Outer generation failure", "Inner Siemens-style import failure", "GenerationExecutionLogTests.Fail" })
                Check(report.Contains(expected), "Missing error context: " + expected);
            Check(!report.Contains("Old output to discard") && report.Length < 46000, "Console capture is not bounded.");
            Check(error.ToString().Contains(Path.GetFullPath(logs[0])) && error.ToString().Contains("Outer generation failure"), "Error report path/original error was not printed.");
            Check(ReferenceEquals(beforeOut, Console.Out) && ReferenceEquals(beforeError, Console.Error), "Console streams were not restored after failure.");
            Check(GenerationExecutionLog.Run("Second run", Fail, () => { throw new IOException("Settings unavailable"); }) == 1, "Context failure hid generation failure.");
            logs = Directory.GetFiles("GenerationErrors", "*.log");
            Check(logs.Length == 2, "A later failure overwrote the earlier report.");
            Check(Array.Exists(logs, f => File.ReadAllText(f).Contains("Settings unavailable")), "Failed settings collection was not recorded.");
            if (Console.IsInputRedirected)
                Check(GenerationExecutionLog.Run("Redirected failure", Fail, pauseOnFailure: true) == 1, "Redirected error pause blocked/changed exit status.");

            // Simulate an unwritable primary location without changing permissions.
            string blocked = Path.Combine(scratch, "blocked"); Directory.CreateDirectory(blocked);
            File.WriteAllText(Path.Combine(blocked, "GenerationErrors"), "A file blocks directory creation");
            Directory.SetCurrentDirectory(blocked);
            error.GetStringBuilder().Clear();
            Check(GenerationExecutionLog.Run("Fallback test", Fail) == 1, "Logging failure changed application failure status.");
            string marker = "Generation failed. Error log: ";
            string console = error.ToString(); int start = console.IndexOf(marker, StringComparison.Ordinal);
            Check(start >= 0, "Fallback report location was not printed.");
            string fallback = console.Substring(start + marker.Length).Trim();
            Check(Path.GetDirectoryName(fallback) == Path.Combine(Path.GetTempPath(), "EtuGeneratorErrors") && File.Exists(fallback), "Temporary fallback log was not created.");
            report = File.ReadAllText(fallback);
            Check(report.Contains("Primary log location unavailable") && report.Contains("Outer generation failure"), "Fallback lost original error/location failure.");
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
            Console.SetOut(originalOut); Console.SetError(originalError);
        }
        Console.WriteLine("PASS: failure reports, bounded stdout/stderr context, nested exceptions/stack traces, nonzero status, unique files, console restoration and temporary fallback; success creates no log. No Portal execution.");
    }
}

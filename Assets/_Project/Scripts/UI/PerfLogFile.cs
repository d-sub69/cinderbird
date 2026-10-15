using System;
using System.IO;
using UnityEngine;

namespace Cinderbird.UI
{
    /// <summary>
    /// The performance lines of this session ([Perf], [Hitch]) in a file next to the save, perf.log, started afresh at
    /// each launch: readable after a run whatever the console or log setup. Testing aid only.
    /// </summary>
    public sealed class PerfLogFile : IDisposable
    {
        private readonly StreamWriter _writer;

        public PerfLogFile(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                Path = System.IO.Path.Combine(folder, "perf.log");
                _writer = new StreamWriter(Path, false) { AutoFlush = true };
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"Couldn't open the performance log: {e.Message}");
            }
        }

        public string Path { get; }

        public void WriteLine(string line)
        {
            try
            {
                _writer?.WriteLine(line);
            }
            catch (IOException)
            {
                // A testing aid must never break the game.
            }
        }

        public void Dispose()
        {
            _writer?.Dispose();
        }
    }
}

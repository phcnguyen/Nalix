namespace Nalix.Diagnostics;

public static partial class Benchmark
{
    /// <summary>
    /// Represents a disposable benchmark unit.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    private sealed class BenchmarkUnit : System.IDisposable
    {
        private readonly string _identifier;
        private bool _isDisposed; // To detect redundant calls
        private System.Diagnostics.Stopwatch _stopwatch = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="BenchmarkUnit" /> class.
        /// </summary>
        /// <param name="identifier">The identifier.</param>
        public BenchmarkUnit(string identifier)
        {
            _identifier = identifier;
            _stopwatch?.Start();
        }

        /// <inheritdoc />
        public void Dispose() => Dispose(true);

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="alsoManaged"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        private void Dispose(bool alsoManaged)
        {
            if (_isDisposed) return;

            if (alsoManaged)
            {
                Add(_identifier, _stopwatch?.Elapsed ?? System.TimeSpan.Zero);
                _stopwatch?.Stop();
            }

            _stopwatch = null;
            _isDisposed = true;
        }
    }
}

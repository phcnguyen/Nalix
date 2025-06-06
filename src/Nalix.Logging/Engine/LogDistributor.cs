using Nalix.Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nalix.Logging.Engine;

/// <summary>
/// High-performance publisher that dispatches log entries to registered logging targets.
/// </summary>
public sealed class LogDistributor : ILogDistributor
{
    #region Fields

    // Using a concurrent dictionary for thread-safe operations on targets
    private readonly ConcurrentDictionary<ILoggerTarget, byte> _targets = new();

    // Use a dummy value (0) for dictionary entries as we only care about the keys
    private const byte DummyValue = 0;

    // Track disposed state in a thread-safe way
    private int _isDisposed;

    // Statistics for monitoring
    private long _entriesDistributor;

    private long _targetsProcessed;
    private long _publishErrorCount;

    #endregion Fields

    #region Properties

    /// <summary>
    /// Gets the total Number of log entries that have been published.
    /// </summary>
    public long EntriesDistributor => Interlocked.Read(ref _entriesDistributor);

    /// <summary>
    /// Gets the total Number of target publish operations performed.
    /// </summary>
    public long TargetsProcessed => Interlocked.Read(ref _targetsProcessed);

    /// <summary>
    /// Gets the Number of errors that occurred during publish operations.
    /// </summary>
    public long PublishErrorCount => Interlocked.Read(ref _publishErrorCount);

    #endregion Properties

    #region Public Methods

    /// <summary>
    /// Publishes a log entry to all registered logging targets.
    /// </summary>
    /// <param name="entry">The log entry to be published.</param>
    /// <exception cref="ArgumentNullException">Thrown if entry is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Publish(LogEntry? entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        // Quick check for disposed state
        ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(LogDistributor));

        // Increment the published entries counter
        Interlocked.Increment(ref _entriesDistributor);

        // Optimize for the common case of a single target
        if (_targets.Count == 1)
        {
            var target = _targets.Keys.First();
            try
            {
                target.Publish(entry.Value);
                Interlocked.Increment(ref _targetsProcessed);
            }
            catch (Exception ex)
            {
                // Count the error but continue operation
                Interlocked.Increment(ref _publishErrorCount);
                HandleTargetError(target, ex, entry.Value);
            }
            return;
        }

        // For multiple targets, publish to each
        foreach (var target in _targets.Keys)
        {
            try
            {
                target.Publish(entry.Value);
                Interlocked.Increment(ref _targetsProcessed);
            }
            catch (Exception ex)
            {
                // Count the error but continue with other targets
                Interlocked.Increment(ref _publishErrorCount);
                HandleTargetError(target, ex, entry.Value);
            }
        }
    }

    /// <summary>
    /// Publishes a log entry asynchronously to all registered logging targets.
    /// </summary>
    /// <param name="entry">The log entry to be published.</param>
    /// <returns>A task representing the asynchronous publish operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if entry is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    public Task PublishAsync(LogEntry? entry)
    {
        if (entry == null)
            throw new ArgumentNullException(nameof(entry));

        // Quick check for disposed state
        ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(LogDistributor));

        // For simplicity and performance, use Task.Run only when there are multiple targets
        // Otherwise just do it synchronously to avoid task allocation overhead
        if (_targets.Count <= 1)
        {
            Publish(entry.Value);
            return Task.CompletedTask;
        }

        return Task.Run(() => Publish(entry.Value));
    }

    /// <summary>
    /// Adds a logging target to receive log entries.
    /// </summary>
    /// <param name="target">The logging target to add.</param>
    /// <returns>The current instance of <see cref="ILogDistributor"/>, allowing method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if target is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    public ILogDistributor AddTarget(ILoggerTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(LogDistributor));

        _targets.TryAdd(target, DummyValue);
        return this;
    }

    /// <summary>
    /// Removes a logging target from the publisher.
    /// </summary>
    /// <param name="target">The logging target to remove.</param>
    /// <returns><c>true</c> if the target was successfully removed; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if target is null.</exception>
    /// <exception cref="ObjectDisposedException">Thrown if this instance is disposed.</exception>
    public bool RemoveTarget(ILoggerTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_isDisposed != 0, nameof(LogDistributor));

        return _targets.TryRemove(target, out _);
    }

    /// <summary>
    /// Handles errors that occur when publishing to a target.
    /// </summary>
    /// <param name="target">The target that caused the error.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="entry">The log entry being published.</param>
    private static void HandleTargetError(ILoggerTarget target, Exception exception, LogEntry entry)
    {
        try
        {
            // Log to debug output at minimum
            Debug.WriteLine(
                $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] Error publishing to " +
                $"{target.GetType().Name}: {exception.Message}");

            // Check if target implements error handling
            if (target is ILoggerErrorHandler errorHandler)
                errorHandler.HandleError(exception, entry);
        }
        catch
        {
            // Ignore errors in the error handler to prevent cascading failures
        }
    }

    /// <summary>
    /// Disposes of the logging publisher and its targets if applicable.
    /// </summary>
    public void Dispose()
    {
        // Thread-safe disposal check
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        try
        {
            // Dispose each target if it implements IDisposable
            foreach (var target in _targets.Keys.OfType<IDisposable>())
            {
                try
                {
                    target.Dispose();
                }
                catch (Exception ex)
                {
                    // Log disposal errors to debug output
                    Debug.WriteLine($"Error disposing logging target: {ex.Message}");
                }
            }

            _targets.Clear();
        }
        catch (Exception ex)
        {
            // Log final disposal errors to debug output
            Debug.WriteLine($"Error during LogDistributor disposal: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a diagnostic report about the publisher's state.
    /// </summary>
    /// <returns>A string containing diagnostic information.</returns>
    public override string ToString()
        => $"[LogDistributor Stats - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}]" + Environment.NewLine +
           $"- User: {Environment.UserName}" + Environment.NewLine +
           $"- Active Targets: {_targets.Count}" + Environment.NewLine +
           $"- Entries Published: {EntriesDistributor:N0}" + Environment.NewLine +
           $"- Target Operations: {TargetsProcessed:N0}" + Environment.NewLine +
           $"- Errors: {PublishErrorCount:N0}" + Environment.NewLine +
           $"- Disposed: {_isDisposed != 0}" + Environment.NewLine;

    #endregion Public Methods
}

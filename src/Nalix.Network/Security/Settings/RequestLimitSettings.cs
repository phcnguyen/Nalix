using System;

namespace Nalix.Network.Security.Settings;

/// <summary>
/// Represents settings for rate limiting, including the Number of requests, lockout duration, and time window.
/// </summary>
public readonly struct RequestLimitSettings
{
    #region Properties

    /// <summary>
    /// Gets the maximum Number of requests allowed.
    /// </summary>
    public int Requests { get; }

    /// <summary>
    /// Gets the duration in seconds to lock out after exceeding the request limit.
    /// </summary>
    public int LockoutDurationSec { get; }

    /// <summary>
    /// Gets the time window in milliseconds for measuring the request rate.
    /// </summary>
    public int TimeWindowMs { get; }

    #endregion Properties

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestLimitSettings"/> struct.
    /// </summary>
    /// <param name="requests">The maximum Number of requests allowed.</param>
    /// <param name="lockoutSeconds">The duration in seconds to lock out after exceeding the request limit.</param>
    /// <param name="windowMilliseconds">The time window in milliseconds for measuring the request rate.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown if any parameter is out of valid range.
    /// </exception>
    public RequestLimitSettings(int requests, int lockoutSeconds, int windowMilliseconds)
    {
        if (requests < 1)
            throw new ArgumentOutOfRangeException(
                nameof(requests), "Requests must be at least 1.");
        if (lockoutSeconds < 0)
            throw new ArgumentOutOfRangeException(
                nameof(lockoutSeconds), "Lockout duration cannot be negative.");
        if (windowMilliseconds < 1)
            throw new ArgumentOutOfRangeException(
                nameof(windowMilliseconds), "Time window must be at least 1 ms.");

        Requests = requests;
        LockoutDurationSec = lockoutSeconds;
        TimeWindowMs = windowMilliseconds;
    }

    #endregion Constructors

    #region Methods

    /// <summary>
    /// Returns a string representation of the settings.
    /// </summary>
    public override string ToString() =>
        $"Requests: {Requests}, Lockout: {LockoutDurationSec}s, Window: {TimeWindowMs}ms";

    #endregion Methods
}

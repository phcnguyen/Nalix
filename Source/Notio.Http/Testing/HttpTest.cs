﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Notio.Http.Configuration;
using Notio.Http.Interfaces;

namespace Notio.Http.Testing;

/// <summary>
/// An object whose existence puts Flurl.Http into test mode where actual HTTP calls are faked. Provides a response
/// queue, call log, and assertion helpers for use in Arrange/Act/Assert style tests.
/// </summary>
[Serializable]
public class HttpTest : HttpTestSetup, ISettingsContainer, IDisposable
{
    private readonly ConcurrentQueue<NotioCall> _calls = new();
    private readonly List<FilteredHttpTestSetup> _filteredSetups = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpTest"/> class.
    /// </summary>
    /// <exception cref="Exception">A delegate callback throws an exception.</exception>
    public HttpTest() : base(new HttpSettings())
    {
        SetCurrentTest(this);
    }

    internal void LogCall(NotioCall call) => _calls.Enqueue(call);

    /// <summary>
    /// Gets the current HttpTest from the logical (async) call context
    /// </summary>
    public static HttpTest Current => GetCurrentTest();

    /// <summary>
    /// List of all (fake) HTTP calls made since this HttpTest was created.
    /// </summary>
    public IReadOnlyList<NotioCall> CallLog => new ReadOnlyCollection<NotioCall>(_calls.ToList());

    /// <summary>
    /// Fluently creates and returns a new request-specific test setup. 
    /// </summary>
    public FilteredHttpTestSetup ForCallsTo(params string[] urlPatterns)
    {
        var setup = new FilteredHttpTestSetup(Settings, urlPatterns);
        _filteredSetups.Add(setup);
        return setup;
    }

    internal HttpTestSetup FindSetup(NotioCall call)
    {
        return _filteredSetups.FirstOrDefault(ts => ts.IsMatch(call)) ?? (HttpTestSetup)this;
    }

    /// <summary>
    /// Asserts whether matching URL was called, throwing HttpCallAssertException if it wasn't.
    /// </summary>
    /// <param name="urlPattern">URL that should have been called. Can include * wildcard character.</param>
    public HttpCallAssertion ShouldHaveCalled(string urlPattern)
    {
        return new HttpCallAssertion(this).WithUrlPattern(urlPattern);
    }

    /// <summary>
    /// Asserts whether matching URL was NOT called, throwing HttpCallAssertException if it was.
    /// </summary>
    /// <param name="urlPattern">URL that should not have been called. Can include * wildcard character.</param>
    public void ShouldNotHaveCalled(string urlPattern)
    {
        new HttpCallAssertion(this, true).WithUrlPattern(urlPattern);
    }

    /// <summary>
    /// Asserts whether any HTTP call was made, throwing HttpCallAssertException if none were.
    /// </summary>
    public HttpCallAssertion ShouldHaveMadeACall()
    {
        return new HttpCallAssertion(this).WithUrlPattern("*");
    }

    /// <summary>
    /// Asserts whether no HTTP calls were made, throwing HttpCallAssertException if any were.
    /// </summary>
    public void ShouldNotHaveMadeACall()
    {
        new HttpCallAssertion(this, true).WithUrlPattern("*");
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    public void Dispose()
    {
        SetCurrentTest(null);
    }

    private static readonly System.Threading.AsyncLocal<HttpTest> _test = new System.Threading.AsyncLocal<HttpTest>();
    private static void SetCurrentTest(HttpTest test) => _test.Value = test;
    private static HttpTest GetCurrentTest() => _test.Value;
}
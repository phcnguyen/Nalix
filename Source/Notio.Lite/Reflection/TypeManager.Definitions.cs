﻿using System;
using System.Collections.Generic;

namespace Notio.Lite.Reflection;

public static partial class TypeManager
{
    /// <summary>
    /// Provides a collection of primitive, numeric types.
    /// </summary>
    public static IReadOnlyList<Type> NumericTypes { get; } =
    [
        typeof(byte),
        typeof(sbyte),
        typeof(decimal),
        typeof(double),
        typeof(float),
        typeof(int),
        typeof(uint),
        typeof(long),
        typeof(ulong),
        typeof(short),
        typeof(ushort),
    ];

    /// <summary>
    /// Provides a collection of basic value types including numeric types,
    /// string, guid, timespan, and datetime.
    /// </summary>
    public static IReadOnlyList<Type> BasicValueTypes { get; } =
    [
        typeof(int),
        typeof(bool),
        typeof(string),
        typeof(DateTime),
        typeof(double),
        typeof(decimal),
        typeof(Guid),
        typeof(long),
        typeof(TimeSpan),
        typeof(uint),
        typeof(float),
        typeof(byte),
        typeof(short),
        typeof(sbyte),
        typeof(ushort),
        typeof(ulong),
        typeof(char),
    ];
}
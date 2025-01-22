﻿using Notio.Common.Connection;
using Notio.Common.Connection.Args;
using System;

namespace Notio.Network.Connection.Args;

/// <summary>
/// Đại diện cho các sự kiện kết nối và cung cấp dữ liệu sự kiện.
/// </summary>
public class ConnectionEventArgs(Connection connection)
    : EventArgs, IConnectEventArgs
{
    /// <summary>
    /// Kết nối liên quan đến sự kiện.
    /// </summary>
    public IConnection Connection { get; } = connection;
}
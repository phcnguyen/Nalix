﻿using Notio.Logging.Enums;
using Notio.Logging.Format;
using Notio.Logging.Interfaces;
using Notio.Logging.Metadata;
using System;

namespace Notio.Logging.Targets;

/// <summary>
/// Lớp ConsoleTarget cung cấp khả năng xuất thông điệp nhật ký ra console với màu sắc tương ứng với mức độ log.
/// </summary>
/// <remarks>
/// Khởi tạo đối tượng ConsoleTarget với định dạng log cụ thể.
/// </remarks>
/// <param name="loggerFormatter">Đối tượng thực hiện định dạng log.</param>
public sealed class ConsoleTarget(ILoggingFormatter loggerFormatter) : ILoggingTarget
{
    private readonly ILoggingFormatter _loggerFormatter = loggerFormatter;

    public ConsoleTarget() : this(new LoggingFormatter())
    {
    }

    /// <summary>
    /// Xuất thông điệp log ra console.
    /// </summary>
    /// <param name="logMessage">Thông điệp log cần xuất.</param>
    public void Publish(LoggingEntry logMessage)
    {
        try
        {
            // Lấy màu tương ứng với mức log
            SetForegroundColor(logMessage.LogLevel);
            Console.WriteLine(_loggerFormatter.FormatLog(logMessage, DateTime.Now));
        }
        finally
        {
            Console.ResetColor(); // Đảm bảo reset màu sau khi in
        }
    }

    /// <summary>
    /// Đặt màu sắc cho mức độ log.
    /// </summary>
    /// <param name="level">Mức độ log cần đặt màu sắc.</param>
    private static void SetForegroundColor(LoggingLevel level)
    {
        var color = level switch
        {
            LoggingLevel.Trace => ConsoleColor.Gray,
            LoggingLevel.Information => ConsoleColor.White,
            LoggingLevel.Debug => ConsoleColor.Green,
            LoggingLevel.Warning => ConsoleColor.Yellow,
            LoggingLevel.Error => ConsoleColor.Magenta,
            LoggingLevel.Critical => ConsoleColor.Red,
            LoggingLevel.None => ConsoleColor.Cyan,
            _ => ConsoleColor.White, // Mặc định
        };
        Console.ForegroundColor = color;
    }
}
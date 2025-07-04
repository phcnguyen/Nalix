﻿using Nalix.Shared.Serialization;
using Nalix.Shared.Serialization.Formatters;
using Nalix.Shared.Serialization.Formatters.Primitives;
using System;
using Xunit;

namespace Nalix.Tests.Shared.Serialization;

public class LiteSerializer_ObjectTests
{
    // Lớp TestObject có thể serialize với 2 thuộc tính: Id (int) và Name (string).
    [Serializable]
    public class TestObject
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public readonly struct TestStruct(int x, float y)
    {
        public readonly int X = x;
        public readonly float Y = y;
    }

    // Kiểm thử serialize/deserialize đối tượng TestObject.
    [Fact]
    public void SerializeDeserialize_Class()
    {
        // Tạo đối tượng đầu vào với Id = 7, Name = "Alice".
        var input = new TestObject { Id = 7, Name = "Alice" };
        // Chuyển đối tượng thành mảng byte.
        byte[] buffer = LiteSerializer.Serialize(input);
        // Khởi tạo output là null để lưu kết quả deserialize.
        TestObject output = null;
        // Chuyển mảng byte về đối tượng.
        LiteSerializer.Deserialize(buffer, ref output);

        // Kiểm tra Id của output khớp với input.
        Assert.Equal(input.Id, output!.Id);
        // Kiểm tra Name của output khớp với input.
        Assert.Equal(input.Name, output.Name);
    }

    // Kiểm thử serialize/deserialize với struct (giá trị).
    [Fact]
    public void SerializeDeserialize_Struct()
    {
        TestStruct input = new(42, 3.14f);
        byte[] buffer = LiteSerializer.Serialize(input);
        TestStruct output = default;
        LiteSerializer.Deserialize(buffer, ref output);

        Assert.Equal(input.X, output.X);
        Assert.Equal(input.Y, output.Y, precision: 3);
    }

    // Optional: kiểm thử serialize/deserialize nullable struct
    [Fact]
    public void SerializeDeserialize_NullableStruct()
    {
        TestStruct? input = new(9, 2.71f);
        byte[] buffer = LiteSerializer.Serialize(input);
        TestStruct? output = null; // Change to nullable TestStruct

        LiteSerializer.Deserialize(buffer, ref output);

        Assert.True(output.HasValue); // This now works because output is nullable
        Assert.Equal(input!.Value.X, output.Value.X);
        Assert.Equal(input.Value.Y, output.Value.Y, precision: 3);
    }

    // Optional: kiểm thử serialize null cho nullable struct
    [Fact]
    public void SerializeDeserialize_NullableStruct_Null()
    {
        TestStruct? input = new();
        byte[] buffer = LiteSerializer.Serialize(input);
        TestStruct? output = new TestStruct(1, 1.1f); // khởi tạo giá trị khác

        LiteSerializer.Deserialize(buffer, ref output);

        Assert.Null(output);
    }

    [Fact]
    public void FormatterProvider_UsesNullableFormatter_ForNullableStruct()
    {
        var formatter = FormatterProvider.Get<TestStruct?>();

        Assert.IsType<NullableFormatter<TestStruct>>(formatter);
    }
}
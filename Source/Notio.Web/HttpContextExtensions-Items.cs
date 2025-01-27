﻿using System;

namespace Notio.Web;

public static partial class HttpContextExtensions
{
    /// <summary>Gets the item associated with the specified key.</summary>
    /// <typeparam name="T">The desired type of the item.</typeparam>
    /// <param name="this">The <see cref="IHttpContext"/> on which this method is called.</param>
    /// <param name="key">The key whose value to get from the <see cref="IHttpContext.Items">Items</see> dictionary.</param>
    /// <param name="value">
    /// <para>When this method returns, the item associated with the specified key,
    /// if the key is found in <see cref="IHttpContext.Items">Items</see>
    /// and the associated value is of type <typeparamref name="T"/>;
    /// otherwise, the default value for <typeparamref name="T"/>.</para>
    /// <para>This parameter is passed uninitialized.</para>
    /// </param>
    /// <returns><see langword="true"/> if the item is found and is of type <typeparamref name="T"/>;
    /// otherwise, <see langword="false"/>.</returns>
    /// <exception cref="NullReferenceException"><paramref name="this"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public static bool TryGetItem<T>(this IHttpContext @this, object key, out T? value)
    {
        if (@this.Items.TryGetValue(key, out var item) && item is T typedItem)
        {
            value = typedItem;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Gets the item associated with the specified key.</summary>
    /// <typeparam name="T">The desired type of the item.</typeparam>
    /// <param name="this">The <see cref="IHttpContext"/> on which this method is called.</param>
    /// <param name="key">The key whose value to get from the <see cref="IHttpContext.Items">Items</see> dictionary.</param>
    /// <returns>The item associated with the specified key,
    /// if the key is found in <see cref="IHttpContext.Items">Items</see>
    /// and the associated value is of type <typeparamref name="T"/>;
    /// otherwise, the default value for <typeparamref name="T"/>.</returns>
    public static T? GetItem<T>(this IHttpContext @this, object key)
        => @this.Items.TryGetValue(key, out var item) && item is T typedItem ? typedItem : default;
}
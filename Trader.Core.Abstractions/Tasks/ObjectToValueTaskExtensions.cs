﻿namespace System;

public static class ObjectToValueTaskExtensions
{
    /// <inheritdoc cref="ValueTask.FromResult{TResult}(TResult)"/>
    public static ValueTask<T> AsValueTaskResult<T>(this T obj)
    {
        return ValueTask.FromResult(obj);
    }

    /// <inheritdoc cref="ValueTask.FromResult{TResult}(TResult)"/>
    public static ValueTask<T?> AsNullableValueTaskResult<T>(this T obj)
    {
        return ValueTask.FromResult<T?>(obj);
    }
}
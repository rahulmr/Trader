﻿using System.Runtime.Serialization;

namespace Outcompute.Trader.Trading.Algorithms.Exceptions;

[Serializable]
public class AlgorithmException : Exception
{
    public AlgorithmException()
    {
    }

    public AlgorithmException(string? message) : base(message)
    {
    }

    public AlgorithmException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    protected AlgorithmException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
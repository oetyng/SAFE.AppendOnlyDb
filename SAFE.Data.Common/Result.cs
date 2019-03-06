using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SAFE.DataClient")]
[assembly: InternalsVisibleTo("SAFE.AppendOnlyDb")]
[assembly: InternalsVisibleTo("SAFE.AppendOnlyDb.Tests")]

namespace SAFE.Data
{
    internal static class Result
    {
        public static Result<T> OK<T>(T value) 
            => new Result<T>(value, true);

        public static Result<T> Fail<T>(int errorCode, string errorMsg)
            => new Result<T>(default, false, errorCode, errorMsg);
    }

    public class Result<T>
    {
        public bool HasValue { get; private set; }
        public string ErrorMsg { get; }
        public int? ErrorCode { get; }
        public T Value { get; private set; }

        internal Result(T value, bool hasValue, int? errorCode = null, string errorMsg = "")
        {
            Value = value;
            HasValue = hasValue;
            ErrorCode = errorCode;
            ErrorMsg = errorMsg ?? string.Empty;
        }
    }

#pragma warning disable SA1502 // Element should not be on a single line
    internal class KeyNotFound<T> : Result<T>
    {
        public KeyNotFound(string info = null)
            : base(default, false, ErrorCodes.KEY_NOT_FOUND, $"Key not found! {info}")
        { }
    }

    internal class ValueDeleted<T> : Result<T>
    {
        public ValueDeleted(string info = null)
            : base(default, false, ErrorCodes.VALUE_DELETED, $"Value deleted! {info}")
        { }
    }

    internal class ValueAlreadyExists<T> : Result<T>
    {
        public ValueAlreadyExists(string info = null)
            : base(default, false, ErrorCodes.VALUE_ALREADY_EXISTS, $"Value already exists! {info}")
        { }
    }

    internal class VersionMismatch<T> : Result<T>
    {
        public VersionMismatch(string info = null)
            : base(default, false, ErrorCodes.VERSION_EXCEPTION, $"Version mismatch! {info}")
        { }
    }

    internal class DeserializationError<T> : Result<T>
    {
        public DeserializationError(string info = null)
            : base(default, false, ErrorCodes.DESERIALIZATION_ERROR, $"Deserialization error! {info}")
        { }
    }

    internal class MdOutOfEntriesError<T> : Result<T>
    {
        public MdOutOfEntriesError(string info = null)
            : base(default, false, ErrorCodes.MD_OUT_OF_ENTRIES, $"Md has no more entries! {info}")
        { }
    }

    internal class ArgumentOutOfRange<T> : Result<T>
    {
        public ArgumentOutOfRange(string info = null)
            : base(default, false, ErrorCodes.ARGUMENT_OUT_OF_RANGE, $"Argument out of range! {info}")
        { }
    }

    internal class MultipleResults<T> : Result<T>
    {
        public MultipleResults(string info = null)
            : base(default, false, ErrorCodes.MULTIPLE_RESULTS, $"Multiple results! {info}")
        { }
    }

    internal class InvalidOperation<T> : Result<T>
    {
        public InvalidOperation(string info = null)
            : base(default, false, ErrorCodes.MULTIPLE_RESULTS, $"Invalid operation! {info}")
        { }
    }

    internal class DataNotFound<T> : Result<T>
    {
        public DataNotFound(string info = null)
            : base(default, false, ErrorCodes.DATA_NOT_FOUND, $"Data not found! {info}")
        { }
    }
    #pragma warning restore SA1502 // Element should not be on a single line
}
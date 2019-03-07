
namespace SAFE.Data
{
    public static class Result
    {
        public static Result<T> OK<T>(T value) 
            => new Result<T>(value, true);

        public static Result<T> Fail<T>(int errorCode, string errorMsg)
            => new Result<T>(default, false, errorCode, errorMsg);

        public static Result<T2> CastError<T1, T2>(this Result<T1> result)
        {
            switch (result)
            {
                case KeyNotFound<T1> r: return new KeyNotFound<T2>(r.ErrorMsg);
                case ValueDeleted<T1> r: return new ValueDeleted<T2>(r.ErrorMsg);
                case ValueAlreadyExists<T1> r: return new ValueAlreadyExists<T2>(r.ErrorMsg);
                case VersionMismatch<T1> r: return new VersionMismatch<T2>(r.ErrorMsg);
                case DeserializationError<T1> r: return new DeserializationError<T2>(r.ErrorMsg);
                case MdOutOfEntriesError<T1> r: return new MdOutOfEntriesError<T2>(r.ErrorMsg);
                case ArgumentOutOfRange<T1> r: return new ArgumentOutOfRange<T2>(r.ErrorMsg);
                case MultipleResults<T1> r: return new MultipleResults<T2>(r.ErrorMsg);
                case InvalidOperation<T1> r: return new InvalidOperation<T2>(r.ErrorMsg);
                case DataNotFound<T1> r: return new DataNotFound<T2>(r.ErrorMsg);
                case var r when !r.HasValue: return Fail<T2>(r.ErrorCode.Value, r.ErrorMsg);
                case var r when r.HasValue: throw new System.NotSupportedException(nameof(result));
                default: throw new System.ArgumentOutOfRangeException(nameof(result));
            }
        }
    }

    public class Result<T>
    {
        public bool HasValue { get; private set; }
        public string ErrorMsg { get; }
        public int? ErrorCode { get; }
        public T Value { get; private set; }

        public Result(T value, bool hasValue, int? errorCode = null, string errorMsg = "")
        {
            Value = value;
            HasValue = hasValue;
            ErrorCode = errorCode;
            ErrorMsg = errorMsg ?? string.Empty;
        }
    }

#pragma warning disable SA1502 // Element should not be on a single line
    public class KeyNotFound<T> : Result<T>
    {
        public KeyNotFound(string info = null)
            : base(default, false, ErrorCodes.KEY_NOT_FOUND, $"Key not found! {info}")
        { }
    }

    public class ValueDeleted<T> : Result<T>
    {
        public ValueDeleted(string info = null)
            : base(default, false, ErrorCodes.VALUE_DELETED, $"Value deleted! {info}")
        { }
    }

    public class ValueAlreadyExists<T> : Result<T>
    {
        public ValueAlreadyExists(string info = null)
            : base(default, false, ErrorCodes.VALUE_ALREADY_EXISTS, $"Value already exists! {info}")
        { }
    }

    public class VersionMismatch<T> : Result<T>
    {
        public VersionMismatch(string info = null)
            : base(default, false, ErrorCodes.VERSION_EXCEPTION, $"Version mismatch! {info}")
        { }
    }

    public class DeserializationError<T> : Result<T>
    {
        public DeserializationError(string info = null)
            : base(default, false, ErrorCodes.DESERIALIZATION_ERROR, $"Deserialization error! {info}")
        { }
    }

    public class MdOutOfEntriesError<T> : Result<T>
    {
        public MdOutOfEntriesError(string info = null)
            : base(default, false, ErrorCodes.MD_OUT_OF_ENTRIES, $"Md has no more entries! {info}")
        { }
    }

    public class ArgumentOutOfRange<T> : Result<T>
    {
        public ArgumentOutOfRange(string info = null)
            : base(default, false, ErrorCodes.ARGUMENT_OUT_OF_RANGE, $"Argument out of range! {info}")
        { }
    }

    public class MultipleResults<T> : Result<T>
    {
        public MultipleResults(string info = null)
            : base(default, false, ErrorCodes.MULTIPLE_RESULTS, $"Multiple results! {info}")
        { }
    }

    public class InvalidOperation<T> : Result<T>
    {
        public InvalidOperation(string info = null)
            : base(default, false, ErrorCodes.MULTIPLE_RESULTS, $"Invalid operation! {info}")
        { }
    }

    public class DataNotFound<T> : Result<T>
    {
        public DataNotFound(string info = null)
            : base(default, false, ErrorCodes.DATA_NOT_FOUND, $"Data not found! {info}")
        { }
    }
    #pragma warning restore SA1502 // Element should not be on a single line
}
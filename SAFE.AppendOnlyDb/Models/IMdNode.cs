using SAFE.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    internal interface IMdNode
    {
        ulong StartIndex { get; }
        ulong EndIndex { get; }
        MdLocator Previous { get; }
        MdLocator Next { get; }

        ExpectedVersion Version { get; }
        int Count { get; }
        bool IsFull { get; }
        int Level { get; }
        MdType Type { get; }
        MdLocator MdLocator { get; }

        // StreamAD
        Task<Result<Pointer>> AppendAsync(StoredValue value);
        Task<Result<StoredValue>> FindAsync(ulong version);
        IAsyncEnumerable<(ulong, StoredValue)> ReadToEndAsync(ulong from);
        IAsyncEnumerable<(ulong, StoredValue)> FindRangeAsync(ulong from, ulong to); // from and to can be any values
        IAsyncEnumerable<StoredValue> GetAllValuesAsync();

        // ValueAD
        Task<Result<StoredValue>> GetLastVersionAsync(); // GetValueAsync

        // StreamAD / ValueAD
        Task<Result<Pointer>> TryAppendAsync(StoredValue value, ExpectedVersion version);
        

        // Internal
        Task<Result<Pointer>> AddAsync(Pointer pointer);
        Task<Result<StoredValue>> GetValueAsync(ulong version);
        IAsyncEnumerable<(Pointer, StoredValue)> GetAllPointerValuesAsync(); // dubious: consider getting for range instead
        Task<Result<(Pointer, StoredValue)>> GetPointerAndValueAsync(ulong version); // for indexing
        Task<Result<bool>> Snapshot<T>(Func<IAsyncEnumerable<T>, byte[]> leftFold); // dubious: consider passing Snapshot class to ctor?
        Task<Result<bool>> SetNext(IMdNode node); // dubious: mixed level of responsibility for setting previous and next
        Task<ulong> GetCount(); // dubious
    }

    public abstract class ExpectedVersion
    {
        public static ExpectedVersion Any => new AnyVersion();
        public static ExpectedVersion None => new NoVersion();

        public ulong? Value { get; }

        protected ExpectedVersion(ulong? version)
        {
            Value = version;
        }

        public static ExpectedVersion Specific(ulong version) => new SpecificVersion(version);

        public override bool Equals(object obj)
        {
            return obj is ExpectedVersion version && 
                obj.GetType() == GetType() &&
                   EqualityComparer<ulong?>.Default.Equals(Value, version.Value);
        }

        public override int GetHashCode()
            => -1937169414 + 
            EqualityComparer<ulong?>.Default.GetHashCode(Value) + 
            EqualityComparer<string>.Default.GetHashCode(GetType().Name);
    }

    public class SpecificVersion : ExpectedVersion
    {
        public SpecificVersion(ulong version)
            : base(version)
        { }

        public static bool operator ==(SpecificVersion s, ExpectedVersion e)
            => e is SpecificVersion && e.Value == s.Value;
        public static bool operator !=(SpecificVersion s, ExpectedVersion e)
            => e is SpecificVersion == false || s.Value != e.Value;

        public static bool operator ==(ExpectedVersion e, SpecificVersion s)
            => e is SpecificVersion && e.Value == s.Value;
        public static bool operator !=(ExpectedVersion e, SpecificVersion s)
            => e is SpecificVersion == false || s.Value != e.Value;

        public override bool Equals(object obj) => base.Equals(obj);
        public override int GetHashCode() => base.GetHashCode();
    }

    public class AnyVersion : ExpectedVersion
    {
        public AnyVersion()
            : base(null)
        { }
    }

    public class NoVersion : ExpectedVersion
    {
        public NoVersion()
            : base(null)
        { }

        public static bool operator ==(NoVersion s, ExpectedVersion e)
           => e is NoVersion && e.Value == s.Value;
        public static bool operator !=(NoVersion s, ExpectedVersion e)
            => e is NoVersion == false || s.Value != e.Value;

        public static bool operator ==(ExpectedVersion e, NoVersion s)
            => e is NoVersion && e.Value == s.Value;
        public static bool operator !=(ExpectedVersion e, NoVersion s)
            => e is NoVersion == false || s.Value != e.Value;

        public override bool Equals(object obj) => base.Equals(obj);
        public override int GetHashCode() => base.GetHashCode();
    }
}
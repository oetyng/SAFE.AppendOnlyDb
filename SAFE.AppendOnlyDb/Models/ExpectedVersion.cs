using System.Collections.Generic;

namespace SAFE.AppendOnlyDb
{
    public abstract class ExpectedVersion
    {
        public static ExpectedVersion Any => new AnyVersion();
        public static ExpectedVersion None => new NoVersion();

        public ulong? Value { get; }

        protected ExpectedVersion(ulong? version) 
            => Value = version;

        public static ExpectedVersion Specific(ulong version) 
            => new SpecificVersion(version);

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
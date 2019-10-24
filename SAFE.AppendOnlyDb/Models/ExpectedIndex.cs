using System.Collections.Generic;

namespace SAFE.AppendOnlyDb
{
    public abstract class ExpectedIndex
    {
        public static ExpectedIndex Any => new AnyIndex();

        public ulong? Value { get; }

        protected ExpectedIndex(ulong? index) 
            => Value = index;

        public static ExpectedIndex Specific(ulong index) 
            => new SpecificIndex(index);

        public override bool Equals(object obj)
        {
            return obj is ExpectedIndex index &&
                obj.GetType() == GetType() &&
                   EqualityComparer<ulong?>.Default.Equals(Value, index.Value);
        }

        public override int GetHashCode()
            => -1937169414 +
            EqualityComparer<ulong?>.Default.GetHashCode(Value) +
            EqualityComparer<string>.Default.GetHashCode(GetType().Name);
    }

    public class AnyIndex : ExpectedIndex
    {
        public AnyIndex()
            : base(null)
        { }
    }

    public class SpecificIndex : ExpectedIndex
    {
        public SpecificIndex(ulong index)
            : base(index)
        { }

        public static bool operator ==(SpecificIndex s, ExpectedIndex e)
            => e is SpecificIndex && e.Value == s.Value;
        public static bool operator !=(SpecificIndex s, ExpectedIndex e)
            => e is SpecificIndex == false || s.Value != e.Value;

        public static bool operator ==(ExpectedIndex e, SpecificIndex s)
            => e is SpecificIndex && e.Value == s.Value;
        public static bool operator !=(ExpectedIndex e, SpecificIndex s)
            => e is SpecificIndex == false || s.Value != e.Value;

        public override bool Equals(object obj) => base.Equals(obj);
        public override int GetHashCode() => base.GetHashCode();
    }
}
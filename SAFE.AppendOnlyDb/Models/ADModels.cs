using System;
using System.Linq;

namespace SAFE.AppendOnlyDb
{
    struct Entry
    {
        public byte[] Key { get; set; }
        public byte[] Value { get; set; }
    }

    public struct IndexFromEnd : IComparable
    {
        public IndexFromEnd(ulong indexFromEnd, ulong count)
        {
            Value = indexFromEnd;
            ulong index;
            checked { index = count - indexFromEnd; }
            AbsoluteIndex = new Index(index);
        }

        public ulong Value { get; }
        public Index AbsoluteIndex { get; }

        public int CompareTo(object obj)
        {
            if (obj is Index index)
            {
                if (index.Value > AbsoluteIndex.Value) return -1;
                else if (AbsoluteIndex.Value == index.Value) return 0;
                else if (AbsoluteIndex.Value > index.Value) return 1;
                else throw new Exception();
            }
            else if (obj is IndexFromEnd indexFromEnd)
            {
                if (indexFromEnd.Value > Value) return 1;
                else if (Value == indexFromEnd.Value) return 0;
                else if (Value > indexFromEnd.Value) return -1;
                else throw new Exception();
            }
            else return -1;
        }
    }

    public struct Index : IComparable
    {
        public Index(ulong value)
            => Value = value;

        public static Index Zero => new Index(0);
        public ulong Value { get; }
        public Index Next => new Index(Value + 1);

        public int CompareTo(object obj)
        {
            if (!(obj is Index index) || index.Value > Value) return -1;
            else if (Value == index.Value) return 0;
            else if (Value > index.Value) return 1;
            else throw new Exception();
        }

        public static bool operator ==(Index s, Index e)
            => e.Value == s.Value;
        public static bool operator !=(Index s, Index e)
            => s.Value != e.Value;

        public static bool operator >(Index s, Index e)
            => s.Value > e.Value;
        public static bool operator <(Index s, Index e)
            => e.Value > s.Value;

        public static Index operator +(Index s, Index e)
        {
            checked { return new Index(e.Value + s.Value); }
        }

        public static Index operator -(Index s, Index e)
        {
            checked { return new Index(s.Value - e.Value); }
        }


        public override bool Equals(object obj)
            => obj is Index index &&
                   Value == index.Value;

        public override int GetHashCode()
            => -1937169414 + Value.GetHashCode();
    }

    struct Address
    {
        public XorName Name { get; set; }
        public ulong Tag { get; set; }
    }

    struct XorName
    {
        public byte[] Value { get; set; }
    }

    struct Permissions { }

    struct Owner
    {
        public PublicKey PublicKey { get; set; }
        public ulong EntriesIndex { get; set; }
        public ulong PermissionsIndex { get; set; }
    }

    struct PublicKey
    {
        public byte[] Value { get; set; }

        public override bool Equals(object obj)
            => obj is PublicKey && Equals((PublicKey)obj);

        public bool Equals(PublicKey other)
            => Enumerable.SequenceEqual(Value, other.Value);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString();

        public static bool operator ==(PublicKey publicKey, PublicKey other)
            => publicKey.Equals(other);

        public static bool operator !=(PublicKey publicKey, PublicKey other)
            => !publicKey.Equals(other);
    }
}

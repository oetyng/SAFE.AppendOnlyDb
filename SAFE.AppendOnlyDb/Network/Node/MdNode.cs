using System;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network
{
    internal sealed partial class MdNode : IMdNode
    {
        readonly IMdDataOps _dataOps;
        MdMetadata _metadata;
        int _count;

        ulong NextVersion => StartIndex + (ulong)Count;

        public ushort Capacity => Constants.MdCapacity;
        public int Count => _count;
        public bool IsFull => Count >= Capacity;

        public byte[] Snapshot => _metadata.Snapshot;

        public ulong StartIndex => _metadata.StartIndex;
        public ulong EndIndex => StartIndex + (ulong)Math.Pow(Capacity, Level + 1) - 1;
        public MdLocator Previous => _metadata.Previous;
        public MdLocator Next => _metadata.Next;

        public int Level => _metadata.Level;
        public MdType Type => Level > 0 ? MdType.Pointers : MdType.Values;
        public MdLocator MdLocator => _dataOps.MdLocator;
        public ExpectedVersion Version => Count == 0 ? ExpectedVersion.None : ExpectedVersion.Specific(NextVersion - 1);

        public IMdNodeFactory NodeFactory => _dataOps.NodeFactory;

        public MdNode(IMdDataOps dataOps, Snapshots.Snapshotter snapshotter)
        {
            _dataOps = dataOps;
            _snapshotter = snapshotter;
        }

        public Task Initialize(MdMetadata metadata) => GetOrAddMetadata(metadata);
    }
}
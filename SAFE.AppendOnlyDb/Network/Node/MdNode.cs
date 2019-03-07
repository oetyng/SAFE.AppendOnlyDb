using System;
using System.Threading.Tasks;
using SafeApp;
using SafeApp.Utilities;

namespace SAFE.AppendOnlyDb.Network
{
    internal sealed partial class MdNode : IMdNode
    {
        readonly MDataInfo _mdInfo;
        readonly MdDataOps _dataOps;
        MdMetadata _metadata;
        int _count;

        ulong NextVersion => StartIndex + (ulong)Count;

        public ushort Capacity => Constants.MdCapacity;
        public int Count => _count;
        public bool IsFull => Count >= Capacity;

        public ulong StartIndex => _metadata.StartIndex;
        public ulong EndIndex => StartIndex + (ulong)Math.Pow(Capacity, Level + 1) - 1;
        public MdLocator Previous => _metadata.Previous;
        public MdLocator Next => _metadata.Next;

        public int Level => _metadata.Level;
        public MdType Type => Level > 0 ? MdType.Pointers : MdType.Values;
        public MdLocator MdLocator => new MdLocator(_mdInfo.Name, _mdInfo.TypeTag, _mdInfo.EncKey, _mdInfo.EncNonce);
        public ExpectedVersion Version => Count == 0 ? ExpectedVersion.None : ExpectedVersion.Specific(NextVersion - 1);

        public MdNode(MDataInfo mdInfo, Session session)
        {
            _mdInfo = mdInfo;
            _dataOps = new MdDataOps(session, mdInfo);
        }

        public Task Initialize(MdMetadata metadata) => GetOrAddMetadata(metadata);
    }
}
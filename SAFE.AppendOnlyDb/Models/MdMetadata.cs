namespace SAFE.AppendOnlyDb
{
    internal class MdMetadata
    {
        public int Level; // tree level, set at init
        public ulong StartIndex; // key of first item, set at init
        public byte[] Snapshot;
        public MdLocator Previous; // set at init
        public MdLocator Next; // set when Capacity is reached
    }
}
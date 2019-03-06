namespace SAFE.AppendOnlyDb
{
    internal class MdHead
    {
        public MdHead(IMdNode md, string id)
        {
            Md = md;
            Id = id;
        }

        public IMdNode Md { get; }
        public string Id { get; }
    }
}
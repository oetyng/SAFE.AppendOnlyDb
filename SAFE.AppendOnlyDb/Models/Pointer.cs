namespace SAFE.AppendOnlyDb
{
    public class Pointer
    {
        /// <summary>
        ///  The address of the Md this points at.
        /// </summary>
        public MdLocator MdLocator { get; set; }

        /// <summary>
        /// The key under which the value is stored in that Md.
        /// </summary>
        public string MdKey { get; set; }

        /// <summary>
        /// The type of the value stored.
        /// </summary>
        public string ValueType { get; set; }
    }
}
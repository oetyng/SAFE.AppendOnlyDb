using Newtonsoft.Json;

namespace SAFE.AppendOnlyDb
{
    public class Locator
    {
        [JsonConstructor]
        Locator()
        { }

        public Locator(byte[] xorName, ulong typeTag)
        {
            XORName = xorName;
            TypeTag = typeTag;
        }

        /// <summary>
        /// The address of the data this points at.
        /// </summary>
        public byte[] XORName { get; set; }

        /// <summary>
        /// Type tag
        /// </summary>
        public ulong TypeTag { get; set; }
    }
}
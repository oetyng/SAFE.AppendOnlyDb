
namespace SAFE.AppendOnlyDb
{
    class Constants
    {
        public const string METADATA_KEY = "metadata";
        public const int MdCapacity = 999; // Since 1 entry is reserved for metadata itself
        // < 1kb. All 999 aggregated entries. Can be a nested ImD datamap (i.e. map of a map..).
    }
}
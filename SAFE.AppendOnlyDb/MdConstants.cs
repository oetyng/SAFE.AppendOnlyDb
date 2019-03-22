
namespace SAFE.AppendOnlyDb
{
    class Constants
    {
        public const string METADATA_KEY = "metadata";
        public const string SNAPSHOT_KEY = "snapshot";
        public const int MdCapacity = 998; // Since 1 entry is reserved for metadata itself, and 1 entry for snapshot map.
        // < 1kb. All 998 aggregated entries. Can be a nested ImD datamap (i.e. map of a map..).
    }
}
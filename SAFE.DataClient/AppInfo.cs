
namespace SAFE.Data.Client
{
    public struct AppInfo
    {
        // Summary:
        //     Application identifier.
        public string Id;

        // Summary:
        //     Application scope, null if not present.
        public string Scope;

        // Summary:
        //     Application name.
        public string Name;

        // Summary:
        //     Application provider/vendor (e.g. MaidSafe).
        public string Vendor;
    }
}
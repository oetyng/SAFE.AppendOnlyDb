
namespace SAFE.Data.Client.Auth
{
    public class Credentials
    {
        public Credentials(string locator, string secret)
        {
            Locator = locator;
            Secret = secret;
        }

        public static Credentials Random => new Credentials(AuthHelpers.GetRandomString(10), AuthHelpers.GetRandomString(10));

        public string Locator { get; }

        public string Secret { get; }
    }
}
using System;
using System.Threading.Tasks;
using SAFE.Data.Client.Auth;

namespace SAFE.Data.Client
{
    public class ClientFactory
    {
        const string MOCK_INMEM_KEY = "SAFE_MOCK_IN_MEMORY_STORAGE";
        readonly string _appId;
        readonly Authentication _authentication;
        readonly Func<SafeApp.Session, string, IStorageClient> _factory;

        public ClientFactory(AppInfo appInfo, Func<SafeApp.Session, string, IStorageClient> factory)
        {
            _appId = appInfo.Id;
            _authentication = new Authentication(appInfo);
            _factory = factory;
        }

        /// <summary>
        /// "mock_unlimited_mutations" : true,
        /// "mock_in_memory_storage" : true,
        /// "mock_vault_path" : null
        /// </summary>
        public async Task<IStorageClient> GetMockNetworkClient(Credentials credentials, bool inMem = true)
        {
            if (inMem)
                Environment.SetEnvironmentVariable(MOCK_INMEM_KEY, "true", EnvironmentVariableTarget.Process);
            else
                Environment.SetEnvironmentVariable(MOCK_INMEM_KEY, null, EnvironmentVariableTarget.Process);

            var session = await _authentication.MockAuthenticationAsync(credentials);
            return _factory(session.Value, _appId);
        }

        public async Task<IStorageClient> GetMockNetworkClientViaBrowserAuth()
        {
            // Authentication with the SAFE browser
            await _authentication.AuthenticationWithBrowserAsync();

            var authResponse = InterProcessCom.ReceiveAuthResponse();

            // Create session from response
            var session = await _authentication.ProcessAuthenticationResponse(authResponse);
            return _factory(session, _appId);
        }

        // Get Alpha-2 / Fleming Client
    }
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SafeApp;
using SafeApp.MockAuthBindings;
using SafeApp.Utilities;

namespace SAFE.Data.Client.Auth
{
    public class Authentication
    {
        readonly AppInfo _appInfo;

        public Authentication(AppInfo appInfo) 
            => _appInfo = appInfo;

        public Task<Result<Session>> MockAuthenticationAsync(Credentials credentials)
        {
            // credentials ??= new Credentials(AuthHelpers.GetRandomString(10), AuthHelpers.GetRandomString(10));
            credentials = credentials ?? new Credentials(AuthHelpers.GetRandomString(10), AuthHelpers.GetRandomString(10));

            var authReq = new AuthReq
            {
                App = new AppExchangeInfo { Id = _appInfo.Id, Name = _appInfo.Name, Scope = _appInfo.Scope, Vendor = _appInfo.Vendor },
                AppContainer = true,
                Containers = new List<ContainerPermissions>()
            };

            return MockAuthenticationAsync(credentials.Locator, credentials.Secret, authReq);
        }

        internal Task<Result<Session>> MockAuthenticationAsync(AuthReq authReq)
        {
            var locator = AuthHelpers.GetRandomString(10);
            var secret = AuthHelpers.GetRandomString(10);
            return MockAuthenticationAsync(locator, secret, authReq);
        }

        internal async Task<Result<Session>> MockAuthenticationAsync(string locator, string secret, AuthReq authReq)
        {
            await ConfigureSession();

            Authenticator authenticator;

            try
            {
                authenticator = await Authenticator.CreateAccountAsync(locator, secret, AuthHelpers.GetRandomString(5));
            }
            catch
            {
                authenticator = await Authenticator.LoginAsync(locator, secret);
            }

            var (_, reqMsg) = await Session.EncodeAuthReqAsync(authReq);
            var ipcReq = await authenticator.DecodeIpcMessageAsync(reqMsg);
            if (!(ipcReq is AuthIpcReq authIpcReq))
                return new InvalidOperation<Session>($"Could not get {nameof(AuthIpcReq)}");

            var resMsg = await authenticator.EncodeAuthRespAsync(authIpcReq, true);
            var ipcResponse = await Session.DecodeIpcMessageAsync(resMsg);
            if (!(ipcResponse is AuthIpcMsg authResponse))
                return new InvalidOperation<Session>($"Could not get {nameof(AuthIpcMsg)}");

            authenticator.Dispose();

            var session = await Session.AppRegisteredAsync(authReq.App.Id, authResponse.AuthGranted);
            return Result.OK(session);
        }

        public async Task AuthenticationWithBrowserAsync()
        {
            try
            {
                await ConfigureSession();

                // Generate and send auth request to safe-browser for authentication.
                Console.WriteLine("Requesting authentication from Safe browser");
                var encodedReq = await AuthHelpers.GenerateEncodedAppRequestAsync(_appInfo);
                var url = AuthHelpers.UrlFormat(_appInfo, encodedReq.Item2, true);
                var info = new System.Diagnostics.ProcessStartInfo
                {
                    UseShellExecute = true, // not default in netcore, so needs to be set
                    FileName = url
                };
                System.Diagnostics.Process.Start(info);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                throw ex;
            }
        }

        public async Task<Session> ProcessAuthenticationResponse(string authResponse)
        {
            try
            {
                // Decode auth response and initialise a new session
                var encodedRequest = AuthHelpers.GetRequestData(authResponse);
                var decodeResult = await Session.DecodeIpcMessageAsync(encodedRequest);
                if (decodeResult.GetType() == typeof(AuthIpcMsg))
                {
                    Console.WriteLine("Auth Reqest Granted from Authenticator");

                    // Create session object
                    if (decodeResult is AuthIpcMsg ipcMsg)
                    {
                        // Initialise a new session
                        var session = await Session.AppRegisteredAsync(_appInfo.Id, ipcMsg.AuthGranted);
                        return session;
                    }
                    else
                    {
                        Console.WriteLine("Invalid AuthIpcMsg");
                        throw new Exception("Invalid AuthIpcMsg.");
                    }
                }
                else
                {
                    Console.WriteLine("Auth Request is not Granted");
                    throw new Exception("Auth Request not granted.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
                throw ex;
            }
        }

        async Task ConfigureSession()
        {
            var exeName = await Session.GetExeFileStemAsync();
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var configPath = $"{basePath}{exeName}.safe_core.config";
            await Session.SetAdditionalSearchPathAsync(configPath);
        }
    }
}
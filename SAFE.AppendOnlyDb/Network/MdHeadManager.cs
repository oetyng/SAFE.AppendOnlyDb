using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SAFE.Data.Utils;
using SafeApp.Utilities;

namespace SAFE.AppendOnlyDb.Network
{
    public class MdHeadPermissionSettings
    {
        // At least Read-permissions needed, if any permissions are given for MdHead (since the MdHeads can't be accessed otherwise).
        public Dictionary<byte[], PermissionSet> AppContainerPermissions { get; set; } = new Dictionary<byte[], PermissionSet>();
        public Dictionary<byte[], PermissionSet> MdHeadPermissions { get; set; } = new Dictionary<byte[], PermissionSet>();
    }

    internal class MdHeadManager
    {
        static readonly string MD_CONTAINER_KEY = "md_container";
        static readonly List<byte> MD_CONTAINER_KEY_BYTES = MD_CONTAINER_KEY.ToUtfBytes();

        readonly string APP_CONTAINER_PATH;

        readonly ulong _protocol;
        readonly INetworkDataOps _dataOps;
        readonly IMdNodeFactory _nodeFactory;
        readonly MdHeadPermissionSettings _permissions;
        IValueAD _mdContainerSource;
        MdContainer _mdContainer;

        

        public MdHeadManager(INetworkDataOps dataOps, IMdNodeFactory nodeFactory, string appId, ulong protocol, MdHeadPermissionSettings permissions = null)
        {
            APP_CONTAINER_PATH = $"apps/{appId}";
            _protocol = protocol;
            _dataOps = dataOps;
            _nodeFactory = nodeFactory;
            _permissions = permissions ?? new MdHeadPermissionSettings();
        }

        public async Task InitializeManager()
        {
            if (!await ExistsManagerAsync())
            {
                // Create new md head container
                _mdContainer = new MdContainer();

                var mdContainerRoot = await GetNewMdNodeAsync();
                var f = new Factories.DataTreeFactory(_nodeFactory);
                _mdContainerSource = await f.CreateAsync((s) => throw new ArgumentOutOfRangeException("Can only add 999k items to this collection.")); // no expansion func, means we can only create 999 databases with this account.
                var serializedDbContainer = _mdContainerSource.MdLocator.Json();

                // Insert a serialized mdContainer into App Container
                var appContainer = await _dataOps.Session.AccessContainer.GetMDataInfoAsync(APP_CONTAINER_PATH);
                var dbIdCipherBytes = await _dataOps.Session.MDataInfoActions.EncryptEntryKeyAsync(appContainer, MD_CONTAINER_KEY_BYTES);
                var dbCipherBytes = await _dataOps.Session.MDataInfoActions.EncryptEntryValueAsync(appContainer, serializedDbContainer.ToUtfBytes());
                using (var appContEntryActionsH = await _dataOps.Session.MDataEntryActions.NewAsync())
                {
                    await _dataOps.Session.MDataEntryActions.InsertAsync(appContEntryActionsH, dbIdCipherBytes, dbCipherBytes);
                    await _dataOps.Session.MData.MutateEntriesAsync(appContainer, appContEntryActionsH); // <----------------------------------------------    Commit ------------------------
                }

                // Set Permissions
                var version = await _dataOps.Session.MData.GetVersionAsync(appContainer);
                foreach (var pair in _permissions.AppContainerPermissions)
                {
                    var userSignKeyH = await _dataOps.Session.Crypto.SignPubKeyNewAsync(pair.Key);
                    await _dataOps.Session.MData.SetUserPermissionsAsync(appContainer, userSignKeyH, pair.Value, ++version);
                }
            }
            else
                _mdContainer = await LoadDbContainer();
        }

        public async Task<MdHead> GetOrAddHeadAsync(string mdName)
        {
            if (string.IsNullOrEmpty(mdName) || string.IsNullOrWhiteSpace(mdName))
                throw new ArgumentException(nameof(mdName));
            if (mdName.Contains("/"))
                throw new NotSupportedException("Unsupported character '/'.");

            var mdId = $"{_protocol}/{mdName}";

            if (_mdContainer.MdLocators.ContainsKey(mdId))
            {
                var location = _mdContainer.MdLocators[mdId];
                var mdResult = await _nodeFactory.LocateAsync(location);
                return new MdHead(mdResult.Value, mdId);
            }

            var newMdResult = await GetNewMdNodeAsync();

             // add mdHead to mdContainer
             _mdContainer.MdLocators[mdId] = newMdResult.MdLocator;

            var result = await _mdContainerSource.SetAsync(new StoredValue(_mdContainer));
            if (!result.HasValue)
            {
                _mdContainer.MdLocators.Remove(mdId);
                throw new ArgumentException(result.ErrorMsg);
            }

            return new MdHead(newMdResult, mdId);
        }

        async Task<IMdNode> GetNewMdNodeAsync()
        {
            // Create Permissions
            using (var permissionsHandle = await _dataOps.Session.MDataPermissions.NewAsync())
            {
                using (var appSignPkH = await _dataOps.Session.Crypto.AppPubSignKeyAsync())
                    await _dataOps.Session.MDataPermissions.InsertAsync(permissionsHandle, appSignPkH, _dataOps.GetFullPermissions());

                // Set Permissions
                foreach (var pair in _permissions.MdHeadPermissions)
                {
                    var userSignKeyH = await _dataOps.Session.Crypto.SignPubKeyNewAsync(pair.Key);
                    await _dataOps.Session.MDataPermissions.InsertAsync(permissionsHandle, userSignKeyH, pair.Value);
                }

                // New mdHead
                var mdInfo = await _dataOps.CreateEmptyRandomPrivateMd(permissionsHandle, DataProtocol.DEFAULT_AD_PROTOCOL); // TODO: DataProtocol.MD_HEAD);
                var locator = new MdLocator(mdInfo.Name, mdInfo.TypeTag, mdInfo.EncKey, mdInfo.EncNonce);

                var mdResult = await _nodeFactory.LocateAsync(locator);

                if (!mdResult.HasValue)
                    throw new ArgumentException(mdResult.ErrorMsg);
                return mdResult.Value;
            }
        }

        async Task<bool> ExistsManagerAsync()
        {
            // Gets the App Container, then checks if it has any key that equals the encrypted name of "md_container"
            var appCont = await _dataOps.Session.AccessContainer.GetMDataInfoAsync(APP_CONTAINER_PATH);
            var mdKeyCipherBytes = await _dataOps.Session.MDataInfoActions.EncryptEntryKeyAsync(appCont, MD_CONTAINER_KEY_BYTES);
            var keys = await _dataOps.Session.MData.ListKeysAsync(appCont);
            return keys.Any(c => c.Key.SequenceEqual(mdKeyCipherBytes));
        }

        async Task<MdContainer> LoadDbContainer()
        {
            var appContainerInfo = await _dataOps.Session.AccessContainer.GetMDataInfoAsync(APP_CONTAINER_PATH);
            var mdKeyCipherBytes = await _dataOps.Session.MDataInfoActions.EncryptEntryKeyAsync(appContainerInfo, MD_CONTAINER_KEY_BYTES);
            var cipherTxtEntryVal = await _dataOps.Session.MData.GetValueAsync(appContainerInfo, mdKeyCipherBytes);

            var plainTxtEntryVal = await _dataOps.Session.MDataInfoActions.DecryptAsync(appContainerInfo, cipherTxtEntryVal.Item1);
            var mdContainerJson = plainTxtEntryVal.ToUtfString();
            var locator = mdContainerJson.Parse<MdLocator>();

            var dataTreeFactory = new Factories.DataTreeFactory(_nodeFactory);
            var result = await dataTreeFactory.LocateAsync(locator, (s) => throw new ArgumentOutOfRangeException("Can only add 999k items to this collection."));
            _mdContainerSource = result.Value; // makes sense to check for HasValue?
            var value = await _mdContainerSource.GetValueAsync();
            if (value.HasValue)
                return value.Value.Parse<MdContainer>();
            return new MdContainer();
        }

        class MdContainer
        {
            public Dictionary<string, MdLocator> MdLocators { get; set; } = new Dictionary<string, MdLocator>();
        }
    }
}
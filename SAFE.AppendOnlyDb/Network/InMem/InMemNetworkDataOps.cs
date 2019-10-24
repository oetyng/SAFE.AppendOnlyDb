using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb.Network.InMem
{
    public class InMemNetworkDataOps : INetworkDataOps
    {
        readonly List<(byte[], byte[])> _store = new List<(byte[], byte[])>();

        public Task<byte[]> GetImmutableData(byte[] map)
        {
            var res = _store
                .SingleOrDefault(c => Enumerable.SequenceEqual(c.Item1, map)).Item2;
            if (res == null)
                throw new ImmutableDataNotFound();
            return Task.FromResult(res);
        }

        public Task<byte[]> StoreImmutableData(byte[] payload)
        {
            var map = payload.Reverse().ToArray();
            _store.Add((map, payload));
            return Task.FromResult(map);
        }
    }

    public class ImmutableDataNotFound : Exception { }
}

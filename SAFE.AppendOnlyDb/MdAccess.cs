using SAFE.Data;
using System;
using System.Threading.Tasks;

namespace SAFE.AppendOnlyDb
{
    internal class MdAccess
    {
        static Func<MdLocator, Task<Result<IMdNode>>> _locator;
        static Func<MdMetadata, Task<IMdNode>> _creator;

        public static void SetLocator(Func<MdLocator, Task<Result<IMdNode>>> locator)
            => _locator = locator;
        public static void SetCreator(Func<MdMetadata, Task<IMdNode>> creator)
            => _creator = creator;

        public static Task<Result<IMdNode>> LocateAsync(MdLocator location) 
            => _locator(location);
        public static Task<IMdNode> CreateAsync(MdMetadata metadata = null) 
            => _creator(metadata);

        public static void UseInMemoryDb()
        {
            throw new NotSupportedException();
            //SetCreator(level => Task.FromResult(InMemoryMd.Create(level)));
            //SetLocator(location => Task.FromResult(Result.OK(InMemoryMd.Locate(location))));
        }
    }
}
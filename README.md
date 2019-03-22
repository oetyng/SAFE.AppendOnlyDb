## SAFE.AppendOnlyDb

[![Nuget (with prereleases)](https://img.shields.io/nuget/vpre/SAFE.AppendOnlyDb.svg)](https://www.nuget.org/packages/SAFE.AppendOnlyDb)

Implementation of `AppendableData`, according to design found here: [DataStore over AppendableData design](https://safenetforum.org/t/datastore-over-appendabledata-design/27611).

Also a simple EventStore is implemented.

### Bullets:
- `IStreamAD`, `IValueAD` and `MutableCollection<T>` implementations.
- *O log n* retrieval of single data and *O log n* to *O(n)* on ranges from `IStreamAD`.
- *O log n** retrieval of value from `IValueAD` (**by caching current leaf we get O(1) after first request*).
- Range queries on versions.
- Test coverage for confirming the functionality.
- EventStore over `AppendableData`.

### Additional goodies:
- Using new language features, such as `IAsyncEnumerable`, from **C# 8.0**

---------------
*For comparing O log n O(1) etc.:*
![](https://discourse-cdn-sjc1.com/business5/uploads/safenetwork/original/3X/5/a/5a7a9201908307edfbcbbcd5354cdf3c202ae146.jpeg)


## AppendableData
I wanted to try out ideas for how `AppendableData` could be designed, as to be able to build a datastore based on it.

I reused much of the logic from [SAFE.DataStore](https://safenetforum.org/t/code-indexed-and-searchable-data-in-safenetwork/26315/14), but didn't aim at shared libraries as I wanted maximal freedom in tailoring this.

As I was working on various ideas for https://safenetforum.org/t/code-filesystem-unlimited-size-working-on-mock-network/26424/21?u=oetyng I hit a small road block with regards to [Dokan](https://github.com/dokan-dev/dokany/wiki/Installation). Instead I started to look at the WAL (write ahead log) like functionality I wanted to implement there. So, I went ahead with what we know about the coming changes, to try them out.

## Short recap
#### Ordered data tree
The basic idea is the same; we have a data tree, which grows horizontally by adding nodes, and vertically by appending new heads at the root. Just like with the `MD` based datastore, the very first node is both the root and a leaf. The leaf level is 0. Every new head that is added, gets an incremented `Level`.

*Vertical growth:*
![DataTree_TryAppendAsync](https://user-images.githubusercontent.com/32025054/54646997-2f951400-4aa1-11e9-9df4-761395a115b5.png)

*Horisontal growth:*
![MdNode_Expand_level](https://user-images.githubusercontent.com/32025054/54647004-3ae83f80-4aa1-11e9-9e67-6b9b3b55b78b.png)


## New things
There are several differences though:
One is that we automatically have a strict order, by virtue of following the concept of an append only stream with monotonically incremented version. Every appended value is stored in an `AD` (i.e. `MD` under the hood) by a key which is the version number.

#### MutableCollection<T>
Another difference is that we use the `MutableCollection<T>` class, which is a combination of a ValueAD and a StreamAD, as to emulate a mutable collection. The ValueAD is what gives us a constant reference to a collection. The collection (StreamAD) that the ValueAD current version points at, can of course never be mutated. So whenever we want to mutate (other than append), we create a new StreamAD, and add the reference to the ValueAD.

#### Range queries
Very similar to how range queries are performed on `B+Trees` (this data tree is quite similar to one, but does not need balancing); we find a node and follow its links forward or backwards on the same level. One difference is that we also do this in-order traversal on `internal nodes`, and not only on `leaf nodes`. If the data spans multiple internal nodes, it can actually split the range request, and perform traversals of the sub-ranges in parallel.

### API

The API has been changed to reflect the two uses of an AD, so we have:

*IValueAD:*
![IValueAD](https://user-images.githubusercontent.com/32025054/54647015-49365b80-4aa1-11e9-935a-8c917744abc5.png)

*IStreamAD:*
![IStreamAD](https://user-images.githubusercontent.com/32025054/54647020-4e93a600-4aa1-11e9-92e0-cf9c874db21d.png)

### Details

#### Streaming data
The most heavily used new feature here from **C# 8.0** is `IAsyncEnumerable`. This has given a lot cleaner code. But it is far more than obscure developer aesthetics at play; while we before could of course have non blocking requests for collections, we would still need to wait for all of them (without custom implementations) before continuing. `IAsyncEnumerable` lets us yield items as they come in, effectively letting us stream out our data to and from the various points.

When running this with in-memory `MockNetwork`, the performance actually degraded slightly (roughly **30%** - probably due to replacing `Parallel.Foreach` on decrypting of `MD` entries, with tasks). But in a real network setting, with significant network penalty, the benefits of streaming the data will shine clearly, since various steps in a process pipe can be executed in parallel (as data is passed on to next steps without waiting for all to be completed in previous step).

#### Snapshotting
The StreamAD supports a specific pattern for snapshotting, in which one entry in each `MD` is reserved for a snapshot of the 998 data entries (the remaining entry is the meta data).
By supplying a function for applying the events, this can be handled automatically as the `AD` is growing, every time it fills a segment (i.e. an `MD`).
![MdNode_Snapshot](https://user-images.githubusercontent.com/32025054/54647027-58b5a480-4aa1-11e9-9d14-0d442c042d8e.png)

#### Event sourcing
The `ReadForwardFrom` API supports keeping event sourced aggregates handled in a distributed fashion. Aggregates can by this API be kept synched, through requesting new events since their last known version.
This also simplifies building local projections out of network event streams.

#### Store any size of data
The underlying logic determines if the data is too large for an MD entry, and will produce an ImD datamap out of it, recursively until the datamap is small enough for the entry.
This is also resolved automatically when fetching the data, so the user doesn't need to take data size into account.

#### Indexing
There's the default index by version, but other than that any indexing will have to be done on side of the writing. No indexer has been implemented in this solution.

#### Data access
The combination of linking `MDs` at same level, and storing them - ordered by version - in a tree, makes access times as good as you could expect; *O log n*. This is because we can route requests through the tree by looking at the `Level`, and the range parameters.

*Point querying the AppendableData (in role IStreamAD):*
![MdNode_Find_0](https://user-images.githubusercontent.com/32025054/54647040-6408d000-4aa1-11e9-8db8-3b63d85a2efc.png)
*Range querying the AppendableData (in role IStreamAD):*
![MdNode_Find_1](https://user-images.githubusercontent.com/32025054/54647049-6a974780-4aa1-11e9-8001-90c1ab577085.png)

### Remarks
- As the sharp eyed might have noticed, the ValueAD current value retrieval is not O(1) as specified in the design. However, O log n is the second best, so I'm not displeased, and there is a default caching of current leaf node in the DataTree implementation, which effectively gives O(1) on ValueAD data retrieval (after the first request).

- Concurrent writes from different processes is handled by optimistic concurrency, however in-process thread safety has not yet been developed for the writes.

*Nice type pattern matching in newer versions of C#, similar to rust:*
![MdNode_Validate_version](https://user-images.githubusercontent.com/32025054/54647090-94506e80-4aa1-11e9-90ba-f97ff5d39923.png)
![StreamADTests_TryAppendAsync_with_wrong_version_fails](https://user-images.githubusercontent.com/32025054/54647096-99152280-4aa1-11e9-86db-296c077549ec.png)

- The GetRangeAsync algorithm is currently not ordering the data as is, so we manage that by ordering at the client when the data has arrived. This can probably be fixed if thinking a little bit more.

- It was a joy to write these tests, because most of it just worked. There were only a few minor adjustments. It's a significant step going from what you envision and have coded, to passing tests, which is that first level of confirmation that the idea works.
(Some do a sort of TDD where you basically cannot write anything without starting with the UnitTests. I find that a bit inhibiting for creativity. You could say that it's a small conflict between the creative mind and the satisfaction in controlled growth, expansion and confirmation of the functionality.)

### Prerequisites

- Visual Studio 2019 with dotnet core development workload

### Supported Platforms

- Windows (x64)

### Required SDK/Tools

- netcore 3.0 SDK
- C# 8.0

## Further Help

Get your developer related questions clarified on the [SAFE Dev Forum](https://forum.safedev.org/). If you're looking to share any other ideas or thoughts on the SAFE Network you can reach out on the [SAFE Network Forum](https://safenetforum.org/).


## Contribution

Copyrights are retained by their contributors. No copyright assignment is required to contribute to this project.


## License

This library is dual-licensed under the Modified BSD ([LICENSE-BSD](LICENSE-BSD) https://opensource.org/licenses/BSD-3-Clause) or the MIT license ([LICENSE-MIT](LICENSE-MIT) https://opensource.org/licenses/MIT) at your option.

The SAFE.DataStore is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the BSD-3 / MIT Licenses for more details.
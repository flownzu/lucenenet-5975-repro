# Lucene.NET 4.8 cannot read deletions written by Lucene.Net 3.0.3 (LUCENE-5975)

Minimal reproduction for [apache/lucenenet#1293](https://github.com/apache/lucenenet/issues/1293).

Lucene.Net 4.8.0-beta00018 fails to open an index written by Lucene.Net 3.0.3 when a segment has
deletions stored as a plain bit vector and its document count is a multiple of 8:

```
System.IO.IOException: did not read all bytes from file: read 10 vs size 11
  (resource: BufferedChecksumIndexInput(MMapIndexInput(path="...\_0_1.del")))
   at Lucene.Net.Codecs.CodecUtil.CheckEOF(IndexInput in)
   at Lucene.Net.Codecs.Lucene40.BitVector..ctor(Directory d, String name, IOContext context)
   at Lucene.Net.Codecs.Lucene40.Lucene40LiveDocsFormat.ReadLiveDocs(...)
   at Lucene.Net.Index.SegmentReader..ctor(...)
```

`IndexWriter` hits the same exception as soon as it applies a delete to such a segment, so the
index can be neither searched nor updated.

## Running it

```
dotnet test tests
```

| Test | Result on 4.8.0-beta00018 |
|---|---|
| `Open_IndexWith3xDeletions_ReadsTheLiveDocuments` | fails with the exception above |
| `Delete_FromIndexWith3xDeletions_Succeeds` | fails with the exception above |
| `Open_IndexWith3xDeletionsWithoutThePaddingByte_ReadsTheLiveDocuments` | passes (control) |

## The index

[`index/`](index) was written by Lucene.Net 3.0.3 with [`generator/`](generator): one segment, 16
documents with a single field `id` (`doc00` … `doc15`), `doc03` deleted. To regenerate it
(.NET Framework, Windows):

```
cd generator
dotnet run -- ../index
```

`_0_1.del` is 11 bytes:

```
00 00 00 10   size  = 16
00 00 00 01   count = 1
08 00 00      bits
```

## Cause

Lucene 3.0.3 allocates the bit vector as `(size >> 3) + 1` bytes, Lucene 4.x as `(size + 7) >> 3`.
For a size that is a multiple of 8 the 3.x file therefore carries one extra byte (always zero, it
lies past the last document). Since Lucene 4.8 `BitVector` calls `CodecUtil.CheckEOF` for files
without a checksum footer, including pre-4.0 files, and rejects that byte.

Files in the d-gaps format (few deletions in a large segment) are not affected, so roughly every
eighth 3.x segment with deletions trips over this.

## Upstream fix

[LUCENE-5975](https://issues.apache.org/jira/browse/LUCENE-5975) ("Lucene can't read 3.0-3.3 deleted
documents", fixed in 4.10.1) only checks EOF for versions that are known to be written correctly.
In `BitVector(Directory, string, IOContext)`, 4.8.1 has

```java
if (version >= VERSION_CHECKSUM) {
  CodecUtil.checkFooter(input);
} else {
  CodecUtil.checkEOF(input);
}
```

and 4.10.4 has

```java
if (version >= VERSION_CHECKSUM) {
  CodecUtil.checkFooter(input);
} else if (version >= VERSION_DGAPS_CLEARED) {
  CodecUtil.checkEOF(input);
}
```

`src/Lucene.Net/Codecs/Lucene40/BitVector.cs` on `master` still has the 4.8.1 variant.

using System;
using System.IO;
using Lucene.Net.Analysis.Core;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using NUnit.Framework;
using Directory = Lucene.Net.Store.Directory;

namespace LuceneNet5975Repro
{
    /// <summary>
    /// The index in ../index was written by Lucene.Net 3.0.3 (see ../generator):
    /// one segment with 16 documents, "doc03" deleted, so 15 live documents.
    /// </summary>
    [TestFixture]
    public class Lucene3xDeletionsTest
    {
        private string _indexPath;
        private Directory _directory;

        [SetUp]
        public void SetUp()
        {
            _indexPath = Path.Combine(Path.GetTempPath(), "lucene5975-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_indexPath);
            foreach (var file in System.IO.Directory.GetFiles(Path.Combine(TestContext.CurrentContext.TestDirectory, "index")))
            {
                File.Copy(file, Path.Combine(_indexPath, Path.GetFileName(file)));
            }

            _directory = FSDirectory.Open(_indexPath);
        }

        [TearDown]
        public void TearDown()
        {
            _directory.Dispose();
            try
            {
                System.IO.Directory.Delete(_indexPath, true);
            }
            catch (IOException)
            {
                // A writer that failed to commit can leave write.lock open.
            }
        }

        // Fails with: IOException: did not read all bytes from file: read 10 vs size 11 (... _0_1.del)
        [Test]
        public void Open_IndexWith3xDeletions_ReadsTheLiveDocuments()
        {
            using var reader = DirectoryReader.Open(_directory);

            Assert.That(reader.NumDocs, Is.EqualTo(15));
            Assert.That(new IndexSearcher(reader).Search(new TermQuery(new Term("id", "doc03")), 1).TotalHits, Is.EqualTo(0));
        }

        // Control: the file is otherwise valid. Cutting off the one surplus (zero) byte that 3.0.3
        // appended makes 4.8 read it correctly, so only the strict EOF check is in the way.
        [Test]
        public void Open_IndexWith3xDeletionsWithoutThePaddingByte_ReadsTheLiveDocuments()
        {
            using (var stream = new FileStream(Path.Combine(_indexPath, "_0_1.del"), FileMode.Open))
            {
                stream.SetLength(stream.Length - 1);
            }

            using var reader = DirectoryReader.Open(_directory);

            Assert.That(reader.NumDocs, Is.EqualTo(15));
        }

        // Writing fails the same way: applying the delete has to load the old deletions first.
        [Test]
        public void Delete_FromIndexWith3xDeletions_Succeeds()
        {
            var config = new IndexWriterConfig(LuceneVersion.LUCENE_48, new KeywordAnalyzer());
            using (var writer = new IndexWriter(_directory, config))
            {
                writer.DeleteDocuments(new Term("id", "doc05"));
                writer.Commit();
            }

            using var reader = DirectoryReader.Open(_directory);
            Assert.That(reader.NumDocs, Is.EqualTo(14));
        }
    }
}

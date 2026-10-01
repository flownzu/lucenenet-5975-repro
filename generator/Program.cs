// Writes the index in ../index with Lucene.Net 3.0.3:
// 16 documents in one segment, "doc03" deleted.
//
// 16 is a multiple of 8, so 3.0.3 writes the deletions bit vector with
// (16 >> 3) + 1 = 3 bytes while 4.8 expects (16 + 7) >> 3 = 2 bytes.

using System;
using System.IO;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Store;

internal static class Program
{
    private const int DocumentCount = 16;
    private const string DeletedId = "doc03";

    private static void Main(string[] args)
    {
        var path = args.Length > 0 ? args[0] : Path.Combine("..", "index");
        if (System.IO.Directory.Exists(path))
        {
            System.IO.Directory.Delete(path, true);
        }

        using (var directory = FSDirectory.Open(new DirectoryInfo(path)))
        {
            var writer = new IndexWriter(directory, new KeywordAnalyzer(), true, IndexWriter.MaxFieldLength.UNLIMITED);

            for (var i = 0; i < DocumentCount; i++)
            {
                var document = new Document();
                document.Add(new Field("id", "doc" + i.ToString("D2"), Field.Store.YES, Field.Index.NOT_ANALYZED));
                writer.AddDocument(document);
            }
            writer.Commit();

            writer.DeleteDocuments(new Term("id", DeletedId));
            writer.Commit();
            writer.Dispose();
        }

        foreach (var file in new DirectoryInfo(path).GetFiles())
        {
            Console.WriteLine(file.Name + " " + file.Length + " bytes");
        }
    }
}

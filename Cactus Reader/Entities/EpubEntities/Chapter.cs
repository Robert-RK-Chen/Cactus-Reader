using System;
using Windows.Storage;

namespace Cactus_Reader.Entities.EpubEntities
{
    public class Chapter(string name, Uri uri, IStorageFile bookFile)
    {
        public string Name { get; private set; } = name;

        public Uri Uri { get; private set; } = uri;

        public IStorageFile BookFile { get; private set; } = bookFile;
    }
}

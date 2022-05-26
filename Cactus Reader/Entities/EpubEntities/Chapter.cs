using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Cactus_Reader.Entities.EpubEntities
{
    public class Chapter
    {
        public string Name { get; private set; }

        public Uri Uri { get; private set; }

        public IStorageFile BookFile { get; private set; }

        public Chapter(string name, Uri uri, IStorageFile bookFile)
        {
            Name = name;
            Uri = uri;
            BookFile = bookFile;
        }
    }
}

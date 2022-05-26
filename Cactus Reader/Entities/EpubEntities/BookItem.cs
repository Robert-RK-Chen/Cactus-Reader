using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Cactus_Reader.Entities.EpubEntities
{
    public class BookItem
    {
        public StorageFile BookFile { get; private set; }

        public string Name { get; private set; }

        public BookItem(StorageFile bookFile)
        {
            BookFile = bookFile;
            Name = bookFile.Name;
        }
    }
}

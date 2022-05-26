using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Cactus_Reader.Entities.EpubEntities
{
    public class BookInfo
    {
        public StorageFile BookFile
        {
            get; private set;
        }

        public int Chapter { get; private set; }

        public int Position { get; private set; }

        public BookInfo(StorageFile bookFile, int chapter = 0, int position = 0)
        {
            BookFile = bookFile;
            Position = position;
            Chapter = chapter;
        }
    }
}

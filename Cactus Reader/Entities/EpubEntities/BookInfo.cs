using Windows.Storage;

namespace Cactus_Reader.Entities.EpubEntities
{
    public class BookInfo(StorageFile bookFile, int chapter = 0, int position = 0)
    {
        public StorageFile BookFile
        {
            get; private set;
        } = bookFile;

        public int Chapter { get; private set; } = chapter;

        public int Position { get; private set; } = position;
    }
}

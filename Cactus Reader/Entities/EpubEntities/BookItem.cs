using Windows.Storage;

namespace Cactus_Reader.Entities.EpubEntities
{
    public class BookItem(StorageFile bookFile)
    {
        public StorageFile BookFile { get; private set; } = bookFile;

        public string Name { get; private set; } = bookFile.Name;
    }
}

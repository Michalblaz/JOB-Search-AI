using System.IO;

namespace MauiApp1.testowe
{
    public interface IAppPackageFileProvider
    {
        Stream OpenRead(string fileName);
    }
}

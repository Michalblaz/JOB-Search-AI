using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using System.IO;
using System.Threading.Tasks;

namespace MauiApp1.testowe
{
    public class MauiSessionStore : IAppSessionStore
    {
        public string GetString(string key, string defaultValue = "") => Preferences.Default.Get(key, defaultValue);
        public bool GetBool(string key, bool defaultValue = false) => Preferences.Default.Get(key, defaultValue);
        public void SetString(string key, string value) => Preferences.Default.Set(key, value);
        public void SetBool(string key, bool value) => Preferences.Default.Set(key, value);
    }

    public class MauiAppDataPathProvider : IAppDataPathProvider
    {
        public string AppDataDirectory => FileSystem.AppDataDirectory;
    }

    public class MauiAppPackageFileProvider : IAppPackageFileProvider
    {
        public Stream OpenRead(string fileName) => FileSystem.OpenAppPackageFileAsync(fileName).GetAwaiter().GetResult();
    }

    public class MauiUrlLauncher : IUrlLauncher
    {
        public Task OpenAsync(string url) => Launcher.OpenAsync(url);
    }
}

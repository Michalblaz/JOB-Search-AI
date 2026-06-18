using Microsoft.JSInterop;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;

namespace MauiApp1.testowe
{
    public class WebSessionStore : IAppSessionStore
    {
        private readonly ConcurrentDictionary<string, object> _values = new();

        public string GetString(string key, string defaultValue = "")
        {
            return _values.TryGetValue(key, out var value) && value is string stringValue
                ? stringValue
                : defaultValue;
        }

        public bool GetBool(string key, bool defaultValue = false)
        {
            return _values.TryGetValue(key, out var value) && value is bool boolValue
                ? boolValue
                : defaultValue;
        }

        public void SetString(string key, string value) => _values[key] = value;

        public void SetBool(string key, bool value) => _values[key] = value;
    }

    public class WebAppDataPathProvider : IAppDataPathProvider
    {
        private readonly string _appDataDirectory;

        public WebAppDataPathProvider(IWebHostEnvironment environment)
        {
            _appDataDirectory = Path.Combine(environment.ContentRootPath, "App_Data");
        }

        public string AppDataDirectory => _appDataDirectory;
    }

    public class WebAppPackageFileProvider : IAppPackageFileProvider
    {
        private readonly IWebHostEnvironment _environment;

        public WebAppPackageFileProvider(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public Stream OpenRead(string fileName)
        {
            var filePath = Path.Combine(_environment.ContentRootPath, "Resources", "Raw", fileName);
            return File.OpenRead(filePath);
        }
    }

    public class WebUrlLauncher : IUrlLauncher
    {
        private readonly IJSRuntime _jsRuntime;

        public WebUrlLauncher(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        public Task OpenAsync(string url)
        {
            return _jsRuntime.InvokeVoidAsync("open", url, "_blank").AsTask();
        }
    }
}

using System.Threading.Tasks;

namespace MauiApp1.testowe
{
    public interface IUrlLauncher
    {
        Task OpenAsync(string url);
    }
}

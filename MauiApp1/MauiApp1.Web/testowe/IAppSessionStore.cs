namespace MauiApp1.testowe
{
    public interface IAppSessionStore
    {
        string GetString(string key, string defaultValue = "");
        bool GetBool(string key, bool defaultValue = false);
        void SetString(string key, string value);
        void SetBool(string key, bool value);
    }
}

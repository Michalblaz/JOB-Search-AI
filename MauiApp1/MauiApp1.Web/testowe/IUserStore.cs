namespace MauiApp1.testowe
{
    public interface IUserStore
    {
        UserAccountRecord? FindByLogin(string login);
        void SaveAccount(UserAccountRecord account);
        bool AnyAccounts();
    }
}

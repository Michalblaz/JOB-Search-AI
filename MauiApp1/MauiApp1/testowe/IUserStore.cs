namespace MauiApp1.testowe
{
    /// <summary>
    /// Definiuje minimalny kontrakt trwałego przechowywania kont użytkowników.
    /// </summary>
    /// <remarks>
    /// Interfejs pozwala używać tego samego <see cref="JobSearchService"/> z lokalnym plikiem JSON albo bazą PostgreSQL.
    /// Implementacje powinny traktować login bez rozróżniania wielkości liter.
    /// </remarks>
    /// <seealso cref="LocalUserStore"/>
    /// <seealso cref="PostgresUserStore"/>
    public interface IUserStore
    {
        /// <summary>
        /// Wyszukuje konto na podstawie loginu.
        /// </summary>
        /// <param name="login">Login podany przez użytkownika podczas logowania lub rejestracji.</param>
        /// <returns>Rekord konta, jeżeli istnieje; w przeciwnym razie <see langword="null"/>.</returns>
        UserAccountRecord? FindByLogin(string login);

        /// <summary>
        /// Zapisuje nowe konto albo aktualizuje istniejący rekord o tym samym loginie.
        /// </summary>
        /// <param name="account">Kompletny stan konta, profilu, ulubionych ofert i historii wyszukiwania.</param>
        /// <exception cref="ArgumentNullException">Może zostać zgłoszony przez implementację, gdy <paramref name="account"/> jest puste.</exception>
        void SaveAccount(UserAccountRecord account);

        /// <summary>
        /// Sprawdza, czy istnieje przynajmniej jedno zarejestrowane konto.
        /// </summary>
        /// <returns><see langword="true"/>, gdy magazyn zawiera co najmniej jedno konto.</returns>
        bool AnyAccounts();
    }
}

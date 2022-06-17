namespace Server.Arkaine.User
{
    public interface IUserService
    {
        Task<IList<string>?> LoginUserAsync(string username, string password);
    }
}

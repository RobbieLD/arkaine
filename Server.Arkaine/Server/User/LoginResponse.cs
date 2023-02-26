namespace Server.Arkaine.User
{
    public class LoginResponse
    {
        public LoginResponse(string userName, bool isAdmin)
        {
            UserName = userName;
            IsAdmin = isAdmin;
        }

        public string UserName { get; }
        public bool IsAdmin { get; }
    }
}

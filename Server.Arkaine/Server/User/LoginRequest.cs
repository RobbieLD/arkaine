﻿namespace Server.Arkaine.User
{
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool Remember { get; set; }
    }
}

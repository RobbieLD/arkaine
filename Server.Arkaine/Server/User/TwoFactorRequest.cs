﻿namespace Server.Arkaine.User
{
    public class TwoFactorRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool Rememeber { get; set; } = false;
    }
}
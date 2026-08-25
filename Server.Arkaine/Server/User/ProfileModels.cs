using System.Text.Json;

namespace Server.Arkaine.User
{
    public sealed record ProfileResponse(
        string UserName,
        string? Email,
        bool TwoFactorEnabled,
        int RecoveryCodesLeft,
        IReadOnlyList<PasskeyResponse> Passkeys);

    public sealed record TwoFactorSetupResponse(
        bool IsEnabled,
        string? SharedKey,
        string? AuthenticatorUri);

    public sealed record TwoFactorEnableResponse(
        IReadOnlyList<string> RecoveryCodes);

    public sealed record PasskeyResponse(
        string Id,
        string? Name,
        DateTimeOffset CreatedAt,
        bool IsUserVerified,
        bool IsBackedUp,
        bool IsBackupEligible);

    public sealed class TwoFactorCodeRequest
    {
        public string Code { get; set; } = string.Empty;
    }

    public sealed class PasskeyRegistrationRequest
    {
        public JsonElement Credential { get; set; }
        public string? Name { get; set; }
    }
}

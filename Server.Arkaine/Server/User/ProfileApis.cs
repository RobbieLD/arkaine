using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using System.Text.Json;

namespace Server.Arkaine.User
{
    public static class ProfileApis
    {
        private const int MaxPasskeys = 10;
        private const int MaxPasskeyNameLength = 100;
        private const int MaxCredentialIdLength = 1023;
        private const int RecoveryCodeCount = 10;
        private const string AuthenticatorIssuer = "Arkaine";

        public static void RegisterProfileApis(this WebApplication app)
        {
            app.MapGet("/profile",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (HttpContext context, UserManager<IdentityUser> userManager) =>
            {
                var user = await GetCurrentUserAsync(context, userManager);
                if (user is null)
                {
                    return Results.Unauthorized();
                }

                var passkeys = await userManager.GetPasskeysAsync(user);
                return Results.Ok(new ProfileResponse(
                    await userManager.GetUserNameAsync(user) ?? string.Empty,
                    await userManager.GetEmailAsync(user),
                    await userManager.GetTwoFactorEnabledAsync(user),
                    await userManager.CountRecoveryCodesAsync(user),
                    passkeys.Select(ToResponse).ToArray()));
            });

            app.MapGet("/profile/2fa/setup",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (HttpContext context, UserManager<IdentityUser> userManager) =>
            {
                var user = await GetCurrentUserAsync(context, userManager);
                if (user is null)
                {
                    return Results.Unauthorized();
                }

                if (await userManager.GetTwoFactorEnabledAsync(user))
                {
                    return Results.Ok(new TwoFactorSetupResponse(true, null, null));
                }

                var sharedKey = await userManager.GetAuthenticatorKeyAsync(user);
                if (string.IsNullOrWhiteSpace(sharedKey))
                {
                    var resetResult = await userManager.ResetAuthenticatorKeyAsync(user);
                    if (!resetResult.Succeeded)
                    {
                        return Results.Problem(
                            "Unable to prepare two-factor authentication.",
                            statusCode: StatusCodes.Status500InternalServerError);
                    }

                    sharedKey = await userManager.GetAuthenticatorKeyAsync(user);
                }

                if (string.IsNullOrWhiteSpace(sharedKey))
                {
                    return Results.Problem(
                        "Unable to prepare two-factor authentication.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                var accountName = await userManager.GetEmailAsync(user)
                    ?? await userManager.GetUserNameAsync(user)
                    ?? user.Id;
                var authenticatorUri = BuildAuthenticatorUri(accountName, sharedKey);

                return Results.Ok(new TwoFactorSetupResponse(false, sharedKey, authenticatorUri));
            });

            app.MapPost("/profile/2fa/enable",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (TwoFactorCodeRequest request, HttpContext context, UserManager<IdentityUser> userManager) =>
            {
                var user = await GetCurrentUserAsync(context, userManager);
                if (user is null)
                {
                    return Results.Unauthorized();
                }

                if (await userManager.GetTwoFactorEnabledAsync(user))
                {
                    return Results.Conflict(new { message = "Two-factor authentication is already enabled." });
                }

                var code = NormalizeAuthenticatorCode(request.Code);
                if (!IsAuthenticatorCode(code) ||
                    !await userManager.VerifyTwoFactorTokenAsync(
                        user,
                        TokenOptions.DefaultAuthenticatorProvider,
                        code))
                {
                    return Results.BadRequest(new { message = "The authenticator code is invalid." });
                }

                var enableResult = await userManager.SetTwoFactorEnabledAsync(user, true);
                if (!enableResult.Succeeded)
                {
                    return Results.Problem(
                        "Unable to enable two-factor authentication.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount);
                if (recoveryCodes is null)
                {
                    return Results.Problem(
                        "Unable to generate two-factor recovery codes.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                return Results.Ok(new TwoFactorEnableResponse(recoveryCodes.ToArray()));
            });

            app.MapPost("/profile/2fa/disable",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (TwoFactorCodeRequest request, HttpContext context, UserManager<IdentityUser> userManager) =>
            {
                var user = await GetCurrentUserAsync(context, userManager);
                if (user is null)
                {
                    return Results.Unauthorized();
                }

                if (!await userManager.GetTwoFactorEnabledAsync(user))
                {
                    return Results.Conflict(new { message = "Two-factor authentication is not enabled." });
                }

                if (!await VerifyTwoFactorCodeAsync(userManager, user, request.Code))
                {
                    return Results.BadRequest(new { message = "The authenticator or recovery code is invalid." });
                }

                var disableResult = await userManager.SetTwoFactorEnabledAsync(user, false);
                if (!disableResult.Succeeded)
                {
                    return Results.Problem(
                        "Unable to disable two-factor authentication.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                var resetResult = await userManager.ResetAuthenticatorKeyAsync(user);
                if (!resetResult.Succeeded)
                {
                    return Results.Problem(
                        "Two-factor authentication was disabled, but its authenticator key could not be reset.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                return Results.Ok();
            });

            app.MapPost("/profile/passkeys/options",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (
                    HttpContext context,
                    UserManager<IdentityUser> userManager,
                    SignInManager<IdentityUser> signInManager) =>
            {
                var user = await GetCurrentUserAsync(context, userManager);
                if (user is null)
                {
                    return Results.Unauthorized();
                }

                var passkeys = await userManager.GetPasskeysAsync(user);
                if (passkeys.Count >= MaxPasskeys)
                {
                    return Results.Conflict(new { message = $"A maximum of {MaxPasskeys} passkeys can be registered." });
                }

                var userId = await userManager.GetUserIdAsync(user);
                var userName = await userManager.GetUserNameAsync(user) ?? userId;
                var displayName = await userManager.GetEmailAsync(user) ?? userName;
                var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
                {
                    Id = userId,
                    Name = userName,
                    DisplayName = displayName
                });

                return Results.Content(optionsJson, "application/json");
            });

            app.MapPost("/profile/passkeys",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (
                    PasskeyRegistrationRequest request,
                    HttpContext context,
                    UserManager<IdentityUser> userManager,
                    SignInManager<IdentityUser> signInManager,
                    ILoggerFactory loggerFactory) =>
            {
                var user = await GetCurrentUserAsync(context, userManager);
                if (user is null)
                {
                    return Results.Unauthorized();
                }

                if (request.Credential.ValueKind != JsonValueKind.Object)
                {
                    return Results.BadRequest(new { message = "A passkey credential is required." });
                }

                var credentialJson = request.Credential.GetRawText();
                if (credentialJson.Length > PasskeyLimits.MaxCredentialJsonLength)
                {
                    return Results.BadRequest(new { message = "The passkey credential is too large." });
                }

                var name = request.Name?.Trim();
                if (name?.Length > MaxPasskeyNameLength)
                {
                    return Results.BadRequest(new { message = $"The passkey name must be {MaxPasskeyNameLength} characters or fewer." });
                }

                PasskeyAttestationResult result;
                try
                {
                    result = await signInManager.PerformPasskeyAttestationAsync(credentialJson);
                }
                catch (InvalidOperationException exception)
                {
                    loggerFactory
                        .CreateLogger("Server.Arkaine.User.ProfileApis")
                        .LogWarning(exception, "Rejected passkey attestation with invalid ceremony state.");
                    return Results.BadRequest(new { message = "Passkey registration failed." });
                }

                if (!result.Succeeded)
                {
                    return Results.BadRequest(new { message = "Passkey registration failed." });
                }

                var userId = await userManager.GetUserIdAsync(user);
                if (!string.Equals(result.UserEntity.Id, userId, StringComparison.Ordinal))
                {
                    return Results.BadRequest(new { message = "Passkey registration failed." });
                }

                var passkeys = await userManager.GetPasskeysAsync(user);
                if (passkeys.Count >= MaxPasskeys)
                {
                    return Results.Conflict(new { message = $"A maximum of {MaxPasskeys} passkeys can be registered." });
                }

                result.Passkey.Name = string.IsNullOrWhiteSpace(name) ? null : name;
                var addResult = await userManager.AddOrUpdatePasskeyAsync(user, result.Passkey);
                if (!addResult.Succeeded)
                {
                    return Results.Problem(
                        "Unable to save the passkey.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                return Results.Ok(ToResponse(result.Passkey));
            });

            app.MapDelete("/profile/passkeys/{credentialId}",
                [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme, Roles = "User, Admin")]
            async (
                    string credentialId,
                    HttpContext context,
                    UserManager<IdentityUser> userManager) =>
            {
                var user = await GetCurrentUserAsync(context, userManager);
                if (user is null)
                {
                    return Results.Unauthorized();
                }

                if (!TryDecodeBase64Url(credentialId, out var decodedCredentialId))
                {
                    return Results.BadRequest(new { message = "The passkey identifier is invalid." });
                }

                var passkeys = await userManager.GetPasskeysAsync(user);
                if (!passkeys.Any(passkey => passkey.CredentialId.SequenceEqual(decodedCredentialId)))
                {
                    return Results.NotFound();
                }

                var removeResult = await userManager.RemovePasskeyAsync(user, decodedCredentialId);
                if (!removeResult.Succeeded)
                {
                    return Results.Problem(
                        "Unable to remove the passkey.",
                        statusCode: StatusCodes.Status500InternalServerError);
                }

                return Results.NoContent();
            });
        }

        private static async Task<IdentityUser?> GetCurrentUserAsync(
            HttpContext context,
            UserManager<IdentityUser> userManager)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return string.IsNullOrWhiteSpace(userId)
                ? null
                : await userManager.FindByIdAsync(userId);
        }

        private static PasskeyResponse ToResponse(UserPasskeyInfo passkey)
        {
            return new PasskeyResponse(
                EncodeBase64Url(passkey.CredentialId),
                passkey.Name,
                passkey.CreatedAt,
                passkey.IsUserVerified,
                passkey.IsBackedUp,
                passkey.IsBackupEligible);
        }

        private static string BuildAuthenticatorUri(string accountName, string sharedKey)
        {
            var label = $"{AuthenticatorIssuer}:{accountName}";
            return $"otpauth://totp/{Uri.EscapeDataString(label)}" +
                $"?secret={Uri.EscapeDataString(sharedKey)}" +
                $"&issuer={Uri.EscapeDataString(AuthenticatorIssuer)}&algorithm=SHA1&digits=6";
        }

        private static async Task<bool> VerifyTwoFactorCodeAsync(
            UserManager<IdentityUser> userManager,
            IdentityUser user,
            string? value)
        {
            var code = NormalizeAuthenticatorCode(value);
            if (IsAuthenticatorCode(code))
            {
                return await userManager.VerifyTwoFactorTokenAsync(
                    user,
                    TokenOptions.DefaultAuthenticatorProvider,
                    code);
            }

            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 100)
            {
                return false;
            }

            var recoveryCodeResult = await userManager.RedeemTwoFactorRecoveryCodeAsync(user, value.Trim());
            return recoveryCodeResult.Succeeded;
        }

        private static string NormalizeAuthenticatorCode(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace(" ", string.Empty).Replace("-", string.Empty);
        }

        private static bool IsAuthenticatorCode(string value)
        {
            return value.Length == 6 && value.All(character => character is >= '0' and <= '9');
        }

        private static string EncodeBase64Url(byte[] value)
        {
            return Convert.ToBase64String(value)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static bool TryDecodeBase64Url(string value, out byte[] decoded)
        {
            decoded = Array.Empty<byte>();
            if (string.IsNullOrWhiteSpace(value) || value.Length > 2048)
            {
                return false;
            }

            foreach (var character in value)
            {
                if (!IsBase64UrlCharacter(character))
                {
                    return false;
                }
            }

            var base64 = value.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 0:
                    break;
                case 2:
                    base64 += "==";
                    break;
                case 3:
                    base64 += "=";
                    break;
                default:
                    return false;
            }

            try
            {
                decoded = Convert.FromBase64String(base64);
            }
            catch (FormatException)
            {
                return false;
            }

            return decoded.Length > 0 && decoded.Length <= MaxCredentialIdLength;
        }

        private static bool IsBase64UrlCharacter(char value)
        {
            return value is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z'
                or >= '0' and <= '9'
                or '-'
                or '_';
        }
    }
}

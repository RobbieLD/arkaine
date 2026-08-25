using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Server.Arkaine;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Server.Arkaine.Tests
{
    [TestFixture]
    public class AuthenticationCookieTests
    {
        [Test]
        public async Task CookieValidation_AllowsTicketsAfterAProcessRestart()
        {
            var properties = new AuthenticationProperties();
            var principal = new ClaimsPrincipal(new ClaimsIdentity("Cookies"));
            var scheme = new AuthenticationScheme(
                CookieAuthenticationDefaults.AuthenticationScheme,
                null,
                typeof(CookieAuthenticationHandler));
            var options = new CookieAuthenticationOptions();
            var events = new CustomCookieAuthenticationEvent("30");

            var signingInContext = new CookieSigningInContext(
                new DefaultHttpContext(),
                scheme,
                options,
                principal,
                properties,
                new CookieOptions());
            await events.SigningIn(signingInContext);

            var ticket = new AuthenticationTicket(principal, properties, scheme.Name);
            var validationContext = new CookieValidatePrincipalContext(
                new DefaultHttpContext(),
                scheme,
                options,
                ticket);
            await new CustomCookieAuthenticationEvent("30").ValidatePrincipal(validationContext);

            Assert.That(validationContext.Principal, Is.SameAs(principal));
            Assert.That(properties.GetString("LifeTimeKey"), Is.Null);
        }
    }
}

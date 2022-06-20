using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Server.Arkaine
{
    public class CustomCookieAuthenticationEvent : CookieAuthenticationEvents
    {
        private const string TicketIssuedTicks = nameof(TicketIssuedTicks);
        private const string LifeTimeKey = nameof(LifeTimeKey);
        private readonly int _hours;
        private readonly Guid _lifetimeKey;

        public CustomCookieAuthenticationEvent(string hours, Guid lifetimeKey)
        {
            _hours = int.Parse(hours);
            _lifetimeKey = lifetimeKey;
        }

        public override async Task SigningIn(CookieSigningInContext context)
        {
            // Add the max lifetime
            context.Properties.SetString(
                TicketIssuedTicks,
                DateTimeOffset.UtcNow.Ticks.ToString());

            // Add the lifetime key
            context.Properties.SetString(
                LifeTimeKey, _lifetimeKey.ToString());

            await base.SigningIn(context);
        }

        public override async Task ValidatePrincipal(
            CookieValidatePrincipalContext context)
        {
            // Validate max life time
            var ticketIssuedTicksValue = context
                .Properties.GetString(TicketIssuedTicks);

            if (ticketIssuedTicksValue is null ||
                !long.TryParse(ticketIssuedTicksValue, out var ticketIssuedTicks))
            {
                await RejectPrincipalAsync(context);
                return;
            }

            var ticketIssuedUtc =
                new DateTimeOffset(ticketIssuedTicks, TimeSpan.FromHours(0));

            if (DateTimeOffset.UtcNow - ticketIssuedUtc > TimeSpan.FromDays(_hours))
            {
                await RejectPrincipalAsync(context);
                return;
            }

            // Validate lifetime key
            var key = context.Properties.GetString(LifeTimeKey);

            if (key != _lifetimeKey.ToString())
            {
                await RejectPrincipalAsync(context);
            }

            await base.ValidatePrincipal(context);
        }

        private static async Task RejectPrincipalAsync(
            CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
        }
    }
}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Server.Arkaine
{
    public class CustomCookieAuthenticationEvent : CookieAuthenticationEvents
    {
        private const string TicketIssuedTicks = nameof(TicketIssuedTicks);
        private readonly int _days;

        public CustomCookieAuthenticationEvent(string days)
        {
            _days = int.Parse(days);
        }

        public override async Task SigningIn(CookieSigningInContext context)
        {
            // Add the max lifetime
            context.Properties.SetString(
                TicketIssuedTicks,
                DateTimeOffset.UtcNow.Ticks.ToString());

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

            if (DateTimeOffset.UtcNow - ticketIssuedUtc > TimeSpan.FromDays(_days))
            {
                await RejectPrincipalAsync(context);
                return;
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

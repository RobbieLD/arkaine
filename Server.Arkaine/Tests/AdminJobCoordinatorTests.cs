using NUnit.Framework;
using Server.Arkaine.Admin;

namespace Server.Arkaine.Tests
{
    public class AdminJobCoordinatorTests
    {
        [Test]
        public void Coordinator_AllowsOnlyOneJobUntilOwnerReleases()
        {
            var coordinator = new AdminJobCoordinator();

            Assert.That(coordinator.TryAcquire(AdminJobKind.Thumbnails), Is.True);
            Assert.That(coordinator.TryAcquire(AdminJobKind.Conversion), Is.False);

            coordinator.Release(AdminJobKind.Conversion);
            Assert.That(coordinator.TryAcquire(AdminJobKind.Conversion), Is.False);

            coordinator.Release(AdminJobKind.Thumbnails);
            Assert.That(coordinator.TryAcquire(AdminJobKind.Conversion), Is.True);
        }
    }
}

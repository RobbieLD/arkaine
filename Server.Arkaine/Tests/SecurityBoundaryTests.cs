using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Server.Arkaine;
using Server.Arkaine.Ingest;
using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Server.Arkaine.Tests
{
    [TestFixture]
    public class SecurityBoundaryTests
    {
        [TestCase("127.0.0.1")]
        [TestCase("10.0.0.1")]
        [TestCase("172.16.0.1")]
        [TestCase("192.168.1.1")]
        [TestCase("::1")]
        [TestCase("fc00::1")]
        [TestCase("fe80::1")]
        public void IsPublicAddress_RejectsPrivateAndReservedAddresses(string value)
        {
            Assert.That(IPAddress.TryParse(value, out var address), Is.True);
            Assert.That(UrlSafetyValidator.IsPublicAddress(address!), Is.False);
        }

        [TestCase("8.8.8.8")]
        [TestCase("2001:4860:4860::8888")]
        public void IsPublicAddress_AllowsPublicAddresses(string value)
        {
            Assert.That(IPAddress.TryParse(value, out var address), Is.True);
            Assert.That(UrlSafetyValidator.IsPublicAddress(address!), Is.True);
        }

        [Test]
        public async Task IsSafeAsync_RejectsLoopbackUrls()
        {
            Assert.That(
                await UrlSafetyValidator.IsSafeAsync("https://127.0.0.1", CancellationToken.None),
                Is.False);
        }

        [Test]
        public void ThumbnailPathResolver_RejectsTraversalAndRootedPaths()
        {
            var root = Path.Combine(Path.GetTempPath(), "arkaine-tests", Guid.NewGuid().ToString("N"));

            try
            {
                Assert.That(
                    ThumbnailPathResolver.TryResolve(root, "folder/thumb.jpg", out var thumbnailPath),
                    Is.True);
                Assert.That(
                    thumbnailPath,
                    Is.EqualTo(Path.GetFullPath(Path.Combine(root, "folder", "thumb.jpg"))));

                Assert.That(ThumbnailPathResolver.TryResolve(root, "../outside.jpg", out _), Is.False);
                Assert.That(ThumbnailPathResolver.TryResolve(root, ".. /outside.jpg", out _), Is.False);
                Assert.That(ThumbnailPathResolver.TryResolve(root, @"C:\outside.jpg", out _), Is.False);
                Assert.That(ThumbnailPathResolver.TryResolve(root, "/outside.jpg", out _), Is.False);
                Assert.That(ThumbnailPathResolver.TryResolve(root, "folder/bad?.jpg", out _), Is.False);
                Assert.That(ThumbnailPathResolver.TryResolve(root, "folder/CON.jpg", out _), Is.False);
                Assert.That(ThumbnailPathResolver.TryResolve(root, "folder/LPT9.png", out _), Is.False);
            }
            finally
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
        }

        [Test]
        public async Task IpFilter_IgnoresSpoofedForwardedForHeader()
        {
            var invoked = false;
            var filter = new IPFilter(
                _ =>
                {
                    invoked = true;
                    return Task.CompletedTask;
                },
                new[] { IPAddress.Parse("192.0.2.10") },
                NullLogger<IPFilter>.Instance);
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.10");
            context.Request.Headers["X-Forwarded-For"] = "192.0.2.10";

            await filter.Invoke(context);

            Assert.That(invoked, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        }
    }
}

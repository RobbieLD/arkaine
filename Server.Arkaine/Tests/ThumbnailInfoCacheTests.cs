using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.IO;

namespace Server.Arkaine.Tests
{
    public class ThumbnailInfoCacheTests
    {
        private string _dir = string.Empty;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "arkaine-cache-tests", Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
                // A locked temp file must not fail the run.
            }
        }

        private string WriteImage(string name, int width, int height)
        {
            var path = Path.Combine(_dir, name);
            using var image = new Image<Rgba32>(width, height);
            image.SaveAsPng(path);
            return path;
        }

        [Test]
        public void Get_Returns_Dimensions_And_Counts_A_Miss()
        {
            var cache = new ThumbnailInfoCache();
            var path = WriteImage("a.png", 40, 25);

            var info = cache.Get(path);
            var stats = cache.GetStats();

            Assert.IsNotNull(info);
            Assert.AreEqual(40, info!.Width);
            Assert.AreEqual(25, info.Height);
            Assert.AreEqual(1, stats.Entries);
            Assert.AreEqual(1, stats.Misses);
            Assert.AreEqual(0, stats.Hits);
        }

        [Test]
        public void Second_Read_Of_Unchanged_File_Is_A_Hit()
        {
            var cache = new ThumbnailInfoCache();
            var path = WriteImage("a.png", 40, 25);

            cache.Get(path);
            cache.Get(path);
            var stats = cache.GetStats();

            Assert.AreEqual(1, stats.Misses);
            Assert.AreEqual(1, stats.Hits);
            Assert.AreEqual(1, stats.Entries);
        }

        [Test]
        public void Changed_File_Is_Counted_As_Stale_And_Re_Read()
        {
            var cache = new ThumbnailInfoCache();
            var path = WriteImage("a.png", 40, 25);
            cache.Get(path);

            File.Delete(path);
            WriteImage("a.png", 80, 60);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(5));

            var info = cache.Get(path);
            var stats = cache.GetStats();

            Assert.AreEqual(80, info!.Width);
            Assert.AreEqual(60, info.Height);
            Assert.AreEqual(1, stats.Stale);
            Assert.AreEqual(1, stats.Misses);
            Assert.AreEqual(0, stats.Hits);
        }

        [Test]
        public void Missing_File_Returns_Null_And_Counts_Not_Found()
        {
            var cache = new ThumbnailInfoCache();

            var info = cache.Get(Path.Combine(_dir, "nope.png"));
            var stats = cache.GetStats();

            Assert.IsNull(info);
            Assert.AreEqual(1, stats.NotFound);
            Assert.AreEqual(0, stats.Entries);
        }

        [Test]
        public void Unreadable_File_Is_Cached_As_Zero_And_Counted()
        {
            var cache = new ThumbnailInfoCache();
            var path = Path.Combine(_dir, "broken.png");
            File.WriteAllText(path, "this is not an image");

            var info = cache.Get(path);
            var stats = cache.GetStats();

            Assert.AreEqual(0, info!.Width);
            Assert.AreEqual(0, info.Height);
            Assert.AreEqual(1, stats.Unreadable);
            Assert.AreEqual(1, stats.Entries);

            // A corrupt thumbnail must not be re-read on every listing.
            cache.Get(path);
            Assert.AreEqual(1, cache.GetStats().Unreadable);
            Assert.AreEqual(1, cache.GetStats().Hits);
        }

        [Test]
        public void Clear_Empties_The_Cache_But_Keeps_Counters()
        {
            var cache = new ThumbnailInfoCache();
            var path = WriteImage("a.png", 40, 25);
            cache.Get(path);
            cache.Get(path);

            cache.Clear();
            var stats = cache.GetStats();

            Assert.AreEqual(0, stats.Entries);
            Assert.AreEqual(1, stats.Hits);
            Assert.AreEqual(1, stats.Misses);
            Assert.AreEqual(1, stats.Resets);
            Assert.IsNotNull(stats.LastResetUtc);
        }

        [Test]
        public void Stats_Report_A_Capacity()
        {
            var stats = new ThumbnailInfoCache().GetStats();

            Assert.Greater(stats.Capacity, 0);
            Assert.IsNull(stats.LastResetUtc);
            Assert.AreEqual(0, stats.Resets);
        }
    }
}

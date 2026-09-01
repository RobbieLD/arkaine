using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using Server.Arkaine.LocalB2;

namespace Server.Arkaine.Tests;

public class LocalB2StoreTests
{
    [Test]
    public async Task UploadedContentType_PersistsAcrossStoreInstances()
    {
        var root = CreateRoot();
        var firstStore = CreateStore(root);

        await using (var content = new MemoryStream([1, 2, 3]))
        {
            await firstStore.UploadAsync(
                "gallery-alpha/photo.bin",
                "application/custom",
                content,
                CancellationToken.None);
        }

        var secondStore = CreateStore(root);
        var file = await secondStore.GetFileAsync(
            "gallery-alpha/photo.bin",
            CancellationToken.None);

        Assert.That(file, Is.Not.Null);
        Assert.That(file!.ContentType, Is.EqualTo("application/custom"));
        Assert.That(file.FileId, Is.EqualTo(LocalB2Store.GetFileId(file.FileName)));
    }

    [Test]
    public void InternalDirectory_IsNotAValidKey()
    {
        var store = CreateStore(CreateRoot());

        Assert.That(
            () => store.ValidateKey(".local-b2/metadata.json"),
            Throws.TypeOf<LocalB2RequestException>()
                .With.Property(nameof(LocalB2RequestException.StatusCode))
                .EqualTo(StatusCodes.Status400BadRequest));
    }

    private static LocalB2Store CreateStore(string root)
    {
        var options = new LocalB2Options
        {
            RootDirectory = root,
            BucketId = "local-bucket",
            BucketName = "local"
        };
        return new LocalB2Store(options);
    }

    private static string CreateRoot()
    {
        var root = Path.Combine(
            TestContext.CurrentContext.WorkDirectory,
            "artifacts",
            Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(root);
        return root;
    }
}

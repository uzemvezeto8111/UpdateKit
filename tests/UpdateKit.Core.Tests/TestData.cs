namespace UpdateKit.Core.Tests;

internal static class TestData
{
    public static ReleaseAsset Asset(string name = "UpdateKit.zip") =>
        new(name, new Uri($"https://example.test/{name}"), 1024, "application/zip");

    public static ReleaseInfo Release(params ReleaseAsset[] assets) =>
        new(
            id: 42,
            tagName: "v1.2.3",
            name: "UpdateKit 1.2.3",
            body: "Release notes",
            htmlUrl: new Uri("https://github.com/example/UpdateKit/releases/tag/v1.2.3"),
            publishedAt: new DateTimeOffset(2026, 7, 19, 10, 0, 0, TimeSpan.Zero),
            isPrerelease: false,
            isDraft: false,
            assets: assets.Length == 0 ? [Asset()] : assets);
}

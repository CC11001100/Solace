using System.Text.Json.Nodes;

namespace Solace.IntegrationTests;

public sealed partial class SolaceIntegrationTests
{
    [Test]
    public async Task Catalog_GetItemsCatalog_ReturnsOk(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/inventory/catalogv3", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var items = (result!["items"] ?? result["Items"]) as JsonArray;
        await Assert.That(items).IsNotNull();
        await Assert.That(items!.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Catalog_GetRecipes_ReturnsOk(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/recipes", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var crafting = (result!["crafting"] ?? result["Crafting"]) as JsonArray;
        await Assert.That(crafting).IsNotNull();
        await Assert.That(crafting!.Count).IsGreaterThan(0);

        var smelting = (result["smelting"] ?? result["Smelting"]) as JsonArray;
        await Assert.That(smelting).IsNotNull();
        await Assert.That(smelting!.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Catalog_GetJournalCatalog_ReturnsOk(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/journal/catalog", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var items = (result!["items"] ?? result["Items"]) as JsonObject;
        await Assert.That(items).IsNotNull();
        await Assert.That(items!.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Catalog_GetProductsCatalog_ReturnsOk(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/products/catalog", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonArray;
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task EnvironmentSettings_Features_ReturnsOk(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/features", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var workshopEnabled = result!["workshop_enabled"]?.GetValue<bool>();
        await Assert.That(workshopEnabled).IsTrue();

        var challengesEnabled = result["challenges_enabled"]?.GetValue<bool>();
        await Assert.That(challengesEnabled).IsTrue();
    }

    [Test]
    public async Task EnvironmentSettings_Settings_ReturnsOk(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/settings", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var tappableRadius = result!["tappableinteractionradius"]?.GetValue<int>();

        await Assert.That(tappableRadius).IsEqualTo(70);
    }

    [Test]
    public async Task ResourcePack_Get_ReturnsOk(CancellationToken cancellationToken)
    {
        using var response = await _fixture.ApiClient.GetAsync("/api/v1.1/resourcepacks/2020.1217.02/default", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonArray;
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Count).IsGreaterThan(0);

        var pack = result[0] as JsonObject;
        await Assert.That(pack).IsNotNull();

        var relativePath = (pack!["relativePath"] ?? pack["RelativePath"])?.GetValue<string>();
        await Assert.That(relativePath).IsNotNull();
        await Assert.That(relativePath!).StartsWith("availableresourcepack/resourcepacks/");
    }
}

using System.Text.Json.Nodes;

namespace Solace.IntegrationTests;

public sealed partial class SolaceIntegrationTests
{
    [Test]
    public async Task Journal_Get_ReturnsInventoryJournalAndActivityLog(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/player/journal", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var inventoryJournal = (result!["inventoryJournal"] ?? result["InventoryJournal"]) as JsonObject;
        await Assert.That(inventoryJournal).IsNotNull();

        var activityLog = (result["activityLog"] ?? result["ActivityLog"]) as JsonArray;
        await Assert.That(activityLog).IsNotNull();
    }
}

using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;

namespace Solace.IntegrationTests;

public sealed partial class SolaceIntegrationTests
{
    private static readonly Guid AppleItemId = Guid.Parse("bd3db2c5-1dbd-8f99-9064-4547590523ef");
    private static readonly Guid BakedPotatoItemId = Guid.Parse("a1aaafc1-3fe0-118d-15f7-1edec233ce78");

    [Test]
    public async Task Inventory_Get_ReturnsInitialInventory(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/inventory/survival", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var hotbar = (result!["hotbar"] ?? result["Hotbar"]) as JsonArray;
        await Assert.That(hotbar).IsNotNull();
        await Assert.That(hotbar!.Count).IsEqualTo(7);

        var stackableItems = (result["stackableItems"] ?? result["StackableItems"]) as JsonArray;
        await Assert.That(stackableItems).IsNotNull();

        var nonStackableItems = (result["nonStackableItems"] ?? result["NonStackableItems"]) as JsonArray;
        await Assert.That(nonStackableItems).IsNotNull();
    }

    [Test]
    public async Task Inventory_SetHotbar_InvalidLength_ReturnsBadRequest(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        // send 3 items instead of 7
        using var response = await client.PutAsync("/1/api/v1.1/inventory/survival/hotbar", new StringContent("""
            [
                { "Id": "00000000-0000-0000-0000-000000000001", "Count": 1, "InstanceId": null },
                { "Id": "00000000-0000-0000-0000-000000000002", "Count": 1, "InstanceId": null },
                { "Id": "00000000-0000-0000-0000-000000000003", "Count": 1, "InstanceId": null }
            ]
            """, Encoding.UTF8, "application/json"), cancellationToken);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Inventory_SetHotbar_LimitsToInventory(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        var unownedId = Guid.NewGuid();

        // send 7 slots where slot 0 has an unowned item
        using var response = await client.PutAsync("/1/api/v1.1/inventory/survival/hotbar", new StringContent($$"""
            [
                { "Id": "{{unownedId}}", "Count": 1, "InstanceId": null },
                null, null, null, null, null, null
            ]
            """, Encoding.UTF8, "application/json"), cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonArray;
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Count).IsEqualTo(7);

        await Assert.That(result[0]).IsNull();
    }

    [Test]
    public async Task Inventory_Consume_InvalidItem_ReturnsBadRequest(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        var randomItemId = Guid.NewGuid();

        using var response = await client.PostAsync($"/1/api/v1.1/inventory/survival/{randomItemId}/consume", null, cancellationToken);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Inventory_Consume_ItemNotOwned_ReturnsOkWithoutChanges(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        using var response = await client.PostAsync($"/1/api/v1.1/inventory/survival/{BakedPotatoItemId}/consume", null, cancellationToken);

        await Assert.That(response).IsOk();
    }

    [Test]
    public async Task Inventory_Consume_ValidFood_RestoresHealthAndDecrementsCount(CancellationToken cancellationToken)
    {
        await using (var earthDb = EarthDbContext.CreateFromConnection(_fixture.EarthConnectionString))
        {
            // set health to 10
            await earthDb.Profiles
                .Where(p => p.Id == _fixture.ProfileId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Health, 10), cancellationToken);

            var existingItem = await earthDb.StackableItems
                .FirstOrDefaultAsync(i => i.ProfileId == _fixture.ProfileId && i.ItemId == AppleItemId, cancellationToken);

            if (existingItem is not null)
            {
                existingItem.Count = 2;
            }
            else
            {
                earthDb.StackableItems.Add(new StackableItemEF(_fixture.ProfileId, AppleItemId, 2));
            }

            var existingJournal = await earthDb.JournalEntries
                .FirstOrDefaultAsync(j => j.ProfileId == _fixture.ProfileId && j.ItemId == AppleItemId, cancellationToken);

            if (existingJournal is null)
            {
                earthDb.JournalEntries.Add(new ItemJournalEntryEF
                {
                    ProfileId = _fixture.ProfileId,
                    ItemId = AppleItemId,
                    FirstSeen = DateTimeOffset.UtcNow,
                    LastSeen = DateTimeOffset.UtcNow,
                    AmountCollected = 2,
                });
            }

            await earthDb.SaveChangesAsync(cancellationToken);
        }

        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.PostAsync($"/1/api/v1.1/inventory/survival/{AppleItemId}/consume", null, cancellationToken);

        await Assert.That(response).IsOk();

        await using (var earthDb = EarthDbContext.CreateFromConnection(_fixture.EarthConnectionString))
        {
            var profile = await earthDb.Profiles.AsNoTracking().FirstAsync(p => p.Id == _fixture.ProfileId, cancellationToken);
            await Assert.That(profile.Health).IsEqualTo(12);

            var item = await earthDb.StackableItems.AsNoTracking().FirstAsync(i => i.ProfileId == _fixture.ProfileId && i.ItemId == AppleItemId, cancellationToken);
            await Assert.That(item.Count).IsEqualTo(1);
        }
    }
}

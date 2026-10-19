using System.Buffers.Binary;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Solace.Db.Earth;
using Solace.Db.Earth.Models.Player;

namespace Solace.IntegrationTests;

public sealed partial class SolaceIntegrationTests
{
    private static readonly Guid AdventureXpPotionItemId = Guid.Parse("acb80f32-4e45-4f59-a203-c297e717f62f");
    private const string UndyingEvokerMiniFigProductId = "1102200015";

    [Test]
    public async Task Products_GetProductInfo_ScannedBoostMini_ReturnsProductInfo(CancellationToken cancellationToken)
    {
        var boostBytes = new byte[24];
        boostBytes[0] = 2;
        boostBytes[1] = 0;
        BinaryPrimitives.WriteUInt32BigEndian(boostBytes.AsSpan(2, 4), uint.Parse(UndyingEvokerMiniFigProductId));
        BinaryPrimitives.WriteInt64BigEndian(boostBytes.AsSpan(12, 8), 9876543210L);

        var b64 = Convert.ToBase64String(boostBytes).TrimEnd('=');
        var url = "pid.mattel/" + b64;
        var recordBytes = new byte[url.Length + 1];
        recordBytes[0] = 1;
        Encoding.UTF8.GetBytes(url, recordBytes.AsSpan(1));

        var byteNumbers = string.Join(",", recordBytes);
        var payload = $$"""
            {
                "NfcChip": {
                    "Data": [
                        [ {{byteNumbers}} ]
                    ]
                }
            }
            """;

        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.PostAsync(
            "/1/api/v1.1/products/getProductInfo",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var id = (result!["id"] ?? result["Id"])?.GetValue<string>();
        await Assert.That(id).IsEqualTo(UndyingEvokerMiniFigProductId);

        var uniqueId = (result["uniqueId"] ?? result["UniqueId"])?.GetValue<string>();
        await Assert.That(uniqueId).IsEqualTo("9876543210");

        var type = (result["type"] ?? result["Type"])?.GetValue<string>();
        await Assert.That(type).IsEqualTo("NfcMiniFig");
    }

    [Test, NotInParallel("boosts")]
    public async Task Boosts_ActivateMiniFig_ActivatesMiniFigBoost(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        var miniFigTagId = "tag-" + Guid.NewGuid().ToString("N")[..8];

        using var activateResponse = await client.PostAsync(
            $"/1/api/v1.1/boosts/minifigs/{UndyingEvokerMiniFigProductId}/{miniFigTagId}/activate",
            null,
            cancellationToken);

        await Assert.That(activateResponse).IsOk();

        using var boostsResponse = await client.GetAsync("/1/api/v1.1/boosts", cancellationToken);
        await Assert.That(boostsResponse).IsOk();

        var json = await boostsResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var miniFigs = (result!["miniFigs"] ?? result["MiniFigs"]) as JsonArray;
        await Assert.That(miniFigs).IsNotNull();

        var activeFound = false;
        foreach (var miniNode in miniFigs!)
        {
            if (miniNode is JsonObject miniObj)
            {
                var id = (miniObj["productId"] ?? miniObj["ProductId"])?.GetValue<string>();
                if (string.Equals(id, UndyingEvokerMiniFigProductId, StringComparison.OrdinalIgnoreCase))
                {
                    activeFound = true;
                    break;
                }
            }
        }

        await Assert.That(activeFound).IsTrue();
    }

    [Test, NotInParallel("boosts")]
    public async Task Boosts_Potion_ActivateAndDeactivate_UpdatesActiveBoosts(CancellationToken cancellationToken)
    {
        await using (var earthDb = EarthDbContext.CreateFromConnection(_fixture.EarthConnectionString))
        {
            var existingPotion = await earthDb.StackableItems
                .FirstOrDefaultAsync(i => i.ProfileId == _fixture.ProfileId && i.ItemId == AdventureXpPotionItemId, cancellationToken);

            if (existingPotion is not null)
            {
                existingPotion.Count += 2;
            }
            else
            {
                earthDb.StackableItems.Add(new StackableItemEF(_fixture.ProfileId, AdventureXpPotionItemId, 2));
            }

            if (!await earthDb.JournalEntries.AnyAsync(j => j.ProfileId == _fixture.ProfileId && j.ItemId == AdventureXpPotionItemId, cancellationToken))
            {
                var now = DateTimeOffset.UtcNow;
                earthDb.JournalEntries.Add(new Solace.Db.Earth.Models.Player.ItemJournalEntryEF
                {
                    ProfileId = _fixture.ProfileId,
                    ItemId = AdventureXpPotionItemId,
                    FirstSeen = now,
                    LastSeen = now,
                    AmountCollected = 2
                });
            }

            await earthDb.SaveChangesAsync(cancellationToken);
        }

        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        using var activateResponse = await client.PostAsync(
            $"/1/api/v1.1/boosts/potions/{AdventureXpPotionItemId}/activate",
            null,
            cancellationToken);

        await Assert.That(activateResponse).IsOk();

        using var boostsResponse = await client.GetAsync("/1/api/v1.1/boosts", cancellationToken);
        await Assert.That(boostsResponse).IsOk();

        var json = await boostsResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var potions = (result!["potions"] ?? result["Potions"]) as JsonArray;
        await Assert.That(potions).IsNotNull();

        string? boostInstanceId = null;
        foreach (var potionNode in potions!)
        {
            if (potionNode is JsonObject potionObj)
            {
                var itemId = (potionObj["itemId"] ?? potionObj["ItemId"])?.GetValue<string>();
                if (string.Equals(itemId, AdventureXpPotionItemId.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    boostInstanceId = (potionObj["instanceId"] ?? potionObj["InstanceId"])?.GetValue<string>();
                    break;
                }
            }
        }

        await Assert.That(boostInstanceId).IsNotNull();

        using var deactivateResponse = await client.DeleteAsync($"/1/api/v1.1/boosts/{boostInstanceId}", cancellationToken);
        await Assert.That(deactivateResponse).IsOk();

        using var boostsAfterResponse = await client.GetAsync("/1/api/v1.1/boosts", cancellationToken);
        await Assert.That(boostsAfterResponse).IsOk();

        var jsonAfter = await boostsAfterResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var resultAfter = (jsonAfter!["result"] ?? jsonAfter["Result"]) as JsonObject;
        var potionsAfter = (resultAfter!["potions"] ?? resultAfter["Potions"]) as JsonArray;

        var stillActive = false;
        foreach (var potionNode in potionsAfter!)
        {
            if (potionNode is JsonObject potionObj)
            {
                var instId = (potionObj["instanceId"] ?? potionObj["InstanceId"])?.GetValue<string>();
                if (string.Equals(instId, boostInstanceId, StringComparison.OrdinalIgnoreCase))
                {
                    stillActive = true;
                    break;
                }
            }
        }

        await Assert.That(stillActive).IsFalse();
    }

    [Test]
    public async Task Boosts_ActivatePotion_InvalidItem_ReturnsBadRequest(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        var randomId = Guid.NewGuid();

        using var response = await client.PostAsync($"/1/api/v1.1/boosts/potions/{randomId}/activate", null, cancellationToken);
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }
}

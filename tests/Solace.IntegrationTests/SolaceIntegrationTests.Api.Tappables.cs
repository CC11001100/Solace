using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Solace.IntegrationTests;

public sealed partial class SolaceIntegrationTests
{
    [Test]
    public async Task Tappables_GetLocations_SpawnsAndReturnsActiveLocations(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/locations/50.0/14.0", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var activeLocations = (result!["activeLocations"] ?? result["ActiveLocations"]) as JsonArray;
        await Assert.That(activeLocations).IsNotNull();
        await Assert.That(activeLocations!.Count).IsGreaterThan(0);

        var firstLocation = activeLocations[0] as JsonObject;
        await Assert.That(firstLocation).IsNotNull();

        var id = (firstLocation!["id"] ?? firstLocation["Id"])?.GetValue<string>();
        await Assert.That(id).IsNotNull();

        var locationType = (firstLocation["type"] ?? firstLocation["Type"])?.GetValue<string>();
        await Assert.That(locationType).IsNotNull();
    }

    [Test]
    public async Task Tappables_Redeem_ValidTappable_GivesRewardsAndPreventsDoubleRedeem(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        using var locationsResponse = await client.GetAsync("/1/api/v1.1/locations/50.0/14.0", cancellationToken);
        await Assert.That(locationsResponse).IsOk();

        var locationsJson = await locationsResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        var locationsResult = (locationsJson!["result"] ?? locationsJson["Result"]) as JsonObject;
        var activeLocations = (locationsResult!["activeLocations"] ?? locationsResult["ActiveLocations"]) as JsonArray;
        await Assert.That(activeLocations).IsNotNull();

        JsonObject? tappableToRedeem = null;
        foreach (var locNode in activeLocations!)
        {
            if (locNode is JsonObject locObj)
            {
                var locType = (locObj["type"] ?? locObj["Type"])?.GetValue<string>();
                if (string.Equals(locType, "TAPPABLE", StringComparison.OrdinalIgnoreCase))
                {
                    tappableToRedeem = locObj;
                    break;
                }
            }
        }

        await Assert.That(tappableToRedeem).IsNotNull();

        var tappableId = (tappableToRedeem!["id"] ?? tappableToRedeem["Id"])?.GetValue<string>();
        var tileId = (tappableToRedeem["tileId"] ?? tappableToRedeem["TileId"])?.GetValue<string>();
        var coordinate = (tappableToRedeem["coordinate"] ?? tappableToRedeem["Coordinate"]) as JsonObject;
        var lat = (coordinate!["latitude"] ?? coordinate["Latitude"])?.GetValue<double>() ?? 50.0;
        var lon = (coordinate["longitude"] ?? coordinate["Longitude"])?.GetValue<double>() ?? 14.0;

        await Assert.That(tappableId).IsNotNull();
        await Assert.That(tileId).IsNotNull();

        var redeemPayload = $$"""
            {
                "Id": "{{tappableId}}",
                "PlayerCoordinate": {
                    "Latitude": {{lat}},
                    "Longitude": {{lon}}
                }
            }
            """;

        using var redeemResponse = await client.PostAsync(
            $"/1/api/v1.1/tappables/{tileId}",
            new StringContent(redeemPayload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(redeemResponse).IsOk();

        var redeemJson = await redeemResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(redeemJson).IsNotNull();

        var redeemResult = (redeemJson!["result"] ?? redeemJson["Result"]) as JsonObject;
        await Assert.That(redeemResult).IsNotNull();

        var token = (redeemResult!["token"] ?? redeemResult["Token"]) as JsonObject;
        await Assert.That(token).IsNotNull();

        var tokenType = (token!["clientType"] ?? token["ClientType"])?.GetValue<string>();
        await Assert.That(tokenType).IsEqualTo("redeemtappable");

        // attempting to redeem the same tappable again should return 400
        using var doubleRedeemResponse = await client.PostAsync(
            $"/1/api/v1.1/tappables/{tileId}",
            new StringContent(redeemPayload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(doubleRedeemResponse.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Tappables_Redeem_InvalidTileId_ReturnsBadRequest(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);

        var payload = $$"""
            {
                "Id": "{{Guid.NewGuid()}}",
                "PlayerCoordinate": {
                    "Latitude": 50.0,
                    "Longitude": 14.0
                }
            }
            """;

        using var response = await client.PostAsync(
            "/1/api/v1.1/tappables/invalid_tile_format",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Tappables_EncountersState_ReturnsValidStates(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        var encounterId = Guid.NewGuid();

        var payload = $$"""
            {
                "{{encounterId}}": "1_2"
            }
            """;

        using var response = await client.PostAsync(
            "/1/api/v1.1/multiplayer/encounters/state",
            new StringContent(payload, Encoding.UTF8, "application/json"),
            cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var stateEntry = (result![encounterId.ToString()] ?? result[encounterId.ToString().ToUpperInvariant()]) as JsonObject;
        await Assert.That(stateEntry).IsNotNull();

        var state = (stateEntry!["activeEncounterState"] ?? stateEntry["ActiveEncounterState"])?.GetValue<string>();
        await Assert.That(state).IsEqualTo("Pristine");
    }
}

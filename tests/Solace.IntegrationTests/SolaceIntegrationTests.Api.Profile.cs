using System.Net;
using System.Text.Json.Nodes;

namespace Solace.IntegrationTests;

public sealed partial class SolaceIntegrationTests
{
    [Test]
    public async Task Profile_Get_ReturnsProfile(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync($"/1/api/v1.1/player/profile/{_fixture.ProfileId}", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var level = (result!["level"] ?? result["Level"])?.GetValue<int>();
        await Assert.That(level).IsEqualTo(1);

        var totalExperience = (result["totalExperience"] ?? result["TotalExperience"])?.GetValue<int>();
        await Assert.That(totalExperience).IsNotNull();
        await Assert.That(totalExperience!.Value).IsGreaterThanOrEqualTo(0);

        var health = (result["health"] ?? result["Health"])?.GetValue<int>();
        await Assert.That(health).IsNotNull();
        await Assert.That(health!.Value).IsGreaterThan(0);

        var healthPercentage = (result["healthPercentage"] ?? result["HealthPercentage"])?.GetValue<float>();
        await Assert.That(healthPercentage).IsNotNull();
        await Assert.That(healthPercentage!.Value).IsGreaterThan(0.0f);

        var levelDistribution = (result["levelDistribution"] ?? result["LevelDistribution"]) as JsonObject;
        await Assert.That(levelDistribution).IsNotNull();
        await Assert.That(levelDistribution!.Count).IsGreaterThan(0);
    }

    [Test]
    public async Task Profile_Get_InvalidGuid_ReturnsBadRequest(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/player/profile/not-a-valid-guid", cancellationToken);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Profile_GetRubies_ReturnsInitialRubies(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/player/rubies", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"])?.GetValue<int>();
        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Value).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Profile_GetSplitRubies_ReturnsSplitRubies(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.GetAsync("/1/api/v1.1/player/splitRubies", cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var purchased = (result!["purchased"] ?? result["Purchased"])?.GetValue<int>();
        var earned = (result["earned"] ?? result["Earned"])?.GetValue<int>();

        await Assert.That(purchased).IsNotNull();
        await Assert.That(earned).IsNotNull();
        await Assert.That(purchased!.Value).IsGreaterThanOrEqualTo(0);
        await Assert.That(earned!.Value).IsGreaterThanOrEqualTo(0);
    }

    [Test]
    public async Task Profile_ChangeLanguage_ReturnsOk(CancellationToken cancellationToken)
    {
        using var client = await CreateAuthenticatedApiClientAsync(cancellationToken);
        using var response = await client.PostAsync("/1/api/v1.1/player/profile/language", null, cancellationToken);

        await Assert.That(response).IsOk();
    }
}

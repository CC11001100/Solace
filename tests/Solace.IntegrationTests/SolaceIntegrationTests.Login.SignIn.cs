using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Solace.IntegrationTests;

public sealed partial class SolaceIntegrationTests
{
    [Test]
    public async Task Login_ApiServer_SignIn(CancellationToken cancellationToken)
    {
        var (sessionTicket, _, _) = await GetPlayfabSessionTicketAsync(cancellationToken);

        using var response = await _fixture.ApiClient.PostAsync("/1/api/v1.1/player/profile/signin", new StringContent($$"""
            {
                "Latitude": 50.0,
                "Longitude": 14.0,
                "DeviceId": "test-device-id",
                "DeviceOS": "Android",
                "DeviceToken": "test-device-token",
                "Language": "en-US",
                "SessionTicket": "{{sessionTicket}}",
                "Streams": {}
            }
            """, Encoding.UTF8, "application/json"), cancellationToken);

        await Assert.That(response).IsOk();

        var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(json).IsNotNull();

        var result = (json!["result"] ?? json["Result"]) as JsonObject;
        await Assert.That(result).IsNotNull();

        var authToken = (result!["authenticationToken"] ?? result["AuthenticationToken"])?.GetValue<string>();
        await Assert.That(authToken).IsNotNull();
        await Assert.That(authToken).IsNotEmpty();

        var basePath = (result!["basePath"] ?? result["BasePath"])?.GetValue<string>();
        await Assert.That(basePath).IsEqualTo("/1");

        // verify that the token works
        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, $"/1/api/v1.1/player/profile/{_fixture.ProfileId}");
        profileRequest.Headers.Authorization = new AuthenticationHeaderValue("Genoa", authToken);

        using var profileResponse = await _fixture.ApiClient.SendAsync(profileRequest, cancellationToken);
        await Assert.That(profileResponse).IsOk();

        var profileJson = await profileResponse.Content.ReadFromJsonAsync<JsonObject>(cancellationToken);
        await Assert.That(profileJson).IsNotNull();

        var profileResult = (profileJson!["result"] ?? profileJson["Result"]) as JsonObject;
        await Assert.That(profileResult).IsNotNull();
        await Assert.That((profileResult!["level"] ?? profileResult["Level"])?.GetValue<int>()).IsEqualTo(1);
    }

    [Test]
    public async Task Login_ApiServer_SignIn_InvalidSessionTicket(CancellationToken cancellationToken)
    {
        using var response = await _fixture.ApiClient.PostAsync("/1/api/v1.1/player/profile/signin", new StringContent("""
            {
                "Latitude": 50.0,
                "Longitude": 14.0,
                "DeviceId": "test-device-id",
                "DeviceOS": "Android",
                "DeviceToken": "test-device-token",
                "Language": "en-US",
                "SessionTicket": "invalid-ticket",
                "Streams": {}
            }
            """, Encoding.UTF8, "application/json"), cancellationToken);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Login_ApiServer_Unauthenticated_ReturnsUnauthorized(CancellationToken cancellationToken)
    {
        using var profileRequest = new HttpRequestMessage(HttpMethod.Get, $"/1/api/v1.1/player/profile/{_fixture.ProfileId}");
        using var profileResponse = await _fixture.ApiClient.SendAsync(profileRequest, cancellationToken);

        await Assert.That(profileResponse.StatusCode).IsEqualTo(HttpStatusCode.Unauthorized);
    }
}

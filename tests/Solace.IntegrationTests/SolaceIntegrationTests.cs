using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aspire.Hosting.Testing;

namespace Solace.IntegrationTests;

[ClassDataSource<IntegrationTestsFixture>(Shared = SharedType.PerAssembly)]
public sealed partial class SolaceIntegrationTests
{
    private readonly IntegrationTestsFixture _fixture;

    public SolaceIntegrationTests(IntegrationTestsFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<string> GetGenoaAuthTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_fixture.GenoaToken is not null)
        {
            return _fixture.GenoaToken;
        }

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

        _fixture.GenoaToken = authToken;
        return authToken!;
    }

    private async Task<HttpClient> CreateAuthenticatedApiClientAsync(CancellationToken cancellationToken = default)
    {
        var token = await GetGenoaAuthTokenAsync(cancellationToken);
        var client = _fixture.App.CreateHttpClient("api-server", "http");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Genoa", token);
        return client;
    }

    private static string ExtractInputValue(string html, string inputName)
    {
        var match = Regex.Match(
            html,
            $@"<input[^>]*name=""{Regex.Escape(inputName)}""[^>]*value=""([^""]*)""",
            RegexOptions.IgnoreCase,
            matchTimeout: TimeSpan.FromSeconds(1));

        if (!match.Success)
        {
            match = Regex.Match(
                html,
                $@"<input[^>]*value=""([^""]*)""[^>]*name=""{Regex.Escape(inputName)}""",
                RegexOptions.IgnoreCase, matchTimeout:
                TimeSpan.FromSeconds(1));
        }

        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string ExtractFormAction(string html)
    {
        var match = Regex.Match(
            html,
            "<form[^>]*action=\"(?<action>[^\"]*)\"",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
            matchTimeout: TimeSpan.FromSeconds(1));

        return match.Success ? match.Groups["action"].Value : string.Empty;
    }

    private static IEnumerable<KeyValuePair<string, string>> ExtractFormInputs(string html)
    {
        foreach (Match match in Regex.Matches(
            html,
            "<input[^>]*name=\"(?<name>[^\"]+)\"[^>]*value=\"(?<value>[^\"]*)\"",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
            matchTimeout: TimeSpan.FromSeconds(1)))
        {
            yield return new KeyValuePair<string, string>(
                match.Groups["name"].Value,
                match.Groups["value"].Value
            );
        }
    }
}

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CaseFlow.Contracts.Auth;
using CaseFlow.Contracts.Cases;

namespace CaseFlow.IntegrationTests;

public static class ApiClientExtensions
{
    // Mirrors the API's serializer setup - enums as names, camelCase keys.
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // Tokens come from the real demo token endpoint, so these tests also
    // prove that issuance and validation agree with each other.
    public static async Task<HttpClient> AsUserAsync(
        this CaseFlowApiFactory factory,
        string[] roles,
        string? organizationId = null,
        string? userId = null)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/demo/token",
            new DemoTokenRequest(roles, organizationId, userId));
        response.EnsureSuccessStatusCode();

        var token = (await response.Content.ReadFromJsonAsync<DemoTokenResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }

    public static async Task<CaseResponse> CreateCaseAsync(this HttpClient client, string title = "Integration test case")
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/cases",
            new CreateCaseRequest(title, "Created by an integration test.", CasePriority.Normal),
            Json);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CaseResponse>(Json))!;
    }
}

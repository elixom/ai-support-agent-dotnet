using System.Net;
using System.Text;
using System.Text.Json;

namespace backend.Tests;

public class KnowledgeBaseIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public KnowledgeBaseIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListKnowledge_IncludesRowCreatedByApi()
    {
        var createdId = await CreateKnowledgeEntryAsync("List test entry", "general");

        var response = await _client.GetAsync("/api/knowledge");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.Contains(
            document.RootElement.EnumerateArray(),
            item => item.TryGetProperty("id", out var idProp) && idProp.GetGuid() == createdId);
    }

    [Fact]
    public async Task DeleteKnowledge_RemovesEntry_AndReturnsNotFoundOnSecondDelete()
    {
        var id = await CreateKnowledgeEntryAsync("Delete test entry", "technical");

        var deleteResponse = await _client.DeleteAsync($"/api/knowledge/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var secondDeleteResponse = await _client.DeleteAsync($"/api/knowledge/{id}");
        Assert.Equal(HttpStatusCode.NotFound, secondDeleteResponse.StatusCode);

        var afterListResponse = await _client.GetAsync("/api/knowledge");
        afterListResponse.EnsureSuccessStatusCode();

        var afterListContent = await afterListResponse.Content.ReadAsStringAsync();
        using var afterDelete = JsonDocument.Parse(afterListContent);

        Assert.DoesNotContain(
            afterDelete.RootElement.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == id);
    }

    private async Task<Guid> CreateKnowledgeEntryAsync(string content, string category)
    {
        var payload = JsonSerializer.Serialize(new
        {
            content,
            category
        });

        var response = await _client.PostAsync(
            "/api/knowledge",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);

        return document.RootElement.GetProperty("id").GetGuid();
    }
}

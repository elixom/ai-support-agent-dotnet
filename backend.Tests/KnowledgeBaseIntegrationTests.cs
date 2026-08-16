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
    public async Task ListKnowledge_ReturnsSeededRows_FromInMemoryDatabase()
    {
        var response = await _client.GetAsync("/api/knowledge");
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(content);

        Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.True(document.RootElement.GetArrayLength() >= 10);
    }

    [Fact]
    public async Task DeleteKnowledge_RemovesEntry_AndReturnsNotFoundOnSecondDelete()
    {
        var listResponse = await _client.GetAsync("/api/knowledge");
        listResponse.EnsureSuccessStatusCode();

        var listContent = await listResponse.Content.ReadAsStringAsync();
        using var beforeDelete = JsonDocument.Parse(listContent);

        var firstItem = beforeDelete.RootElement.EnumerateArray().First();
        var id = firstItem.GetProperty("id").GetGuid();

        var deleteResponse = await _client.DeleteAsync($"/api/knowledge/{id}");
        Assert.Equal(System.Net.HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var secondDeleteResponse = await _client.DeleteAsync($"/api/knowledge/{id}");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, secondDeleteResponse.StatusCode);

        var afterListResponse = await _client.GetAsync("/api/knowledge");
        afterListResponse.EnsureSuccessStatusCode();

        var afterListContent = await afterListResponse.Content.ReadAsStringAsync();
        using var afterDelete = JsonDocument.Parse(afterListContent);

        Assert.DoesNotContain(
            afterDelete.RootElement.EnumerateArray(),
            item => item.GetProperty("id").GetGuid() == id);
    }
}

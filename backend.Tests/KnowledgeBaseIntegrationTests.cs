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
}

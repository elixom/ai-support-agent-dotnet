using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.Services
{
    public interface IEmbeddingService
    {
        Task<List<float>> GenerateEmbeddingAsync(string text, string? customApiKey = null);
        List<string> ChunkText(string text, int chunkSize = 500, int overlap = 50);
    }
}

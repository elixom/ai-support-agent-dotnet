using System.Collections.Generic;

namespace backend.Services
{
    public class GuardrailResult
    {
        public bool IsSafe { get; set; } = true;
        public List<string> FlaggedTerms { get; set; } = new List<string>();
        public string Recommendation { get; set; } = string.Empty;
    }

    public interface IGuardrailsService
    {
        GuardrailResult CheckResponse(string response, List<string> knowledgeChunks);
    }
}

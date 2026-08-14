using System.Threading.Tasks;

namespace backend.Services
{
    public class ClassificationResult
    {
        public string Category { get; set; } = "general";
        public double Confidence { get; set; } = 0.5;
        public string Reasoning { get; set; } = string.Empty;
    }

    public interface IClassifierService
    {
        Task<ClassificationResult> ClassifyTicketAsync(string message, string? customApiKey = null);
    }
}

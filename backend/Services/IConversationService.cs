using System.Threading.Tasks;

namespace backend.Services
{
    public interface IConversationService
    {
        Task<object> ProcessMessageAsync(string messageText, string senderId, string? senderName, string channel);
    }
}

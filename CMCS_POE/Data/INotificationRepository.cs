using CMCS_POE.Models;

namespace CMCS_POE.Data
{
    public interface INotificationRepository
    {
        List<Notification> GetAll();
        List<Notification> GetUnread();
        Notification? GetById(int id);
        void Add(Notification notification);
        void MarkAsRead(int id);
        void MarkAllAsRead();
        int GetUnreadCount();
        void Delete(int id);
        void DeleteAll();
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].
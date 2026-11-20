using CMCS_POE.Models;
using System.Text.Json;

namespace CMCS_POE.Data
{
    public class NotificationRepository : INotificationRepository
    {
        private static List<Notification> _notifications = new List<Notification>();
        private readonly string _filePath;

        public NotificationRepository()
        {
            _filePath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "notifications.json");
            var directory = Path.GetDirectoryName(_filePath)
                ?? throw new InvalidOperationException("The storage directory could not be resolved.");
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            LoadFromFile();
        }

        public List<Notification> GetAll()
        {
            return _notifications.OrderByDescending(n => n.CreatedDate).ToList();
        }

        public List<Notification> GetUnread()
        {
            return _notifications
                .Where(n => !n.IsRead)
                .OrderByDescending(n => n.CreatedDate)
                .ToList();
        }

        public Notification? GetById(int id)
        {
            return _notifications.FirstOrDefault(n => n.NotificationId == id);
        }

        public void Add(Notification notification)
        {
            notification.NotificationId = _notifications.Any()
                ? _notifications.Max(n => n.NotificationId) + 1
                : 1;
            notification.CreatedDate = DateTime.Now;
            _notifications.Add(notification);
            SaveToFile();
        }

        public void MarkAsRead(int id)
        {
            var notification = GetById(id);
            if (notification != null)
            {
                notification.IsRead = true;
                SaveToFile();
            }
        }

        public void MarkAllAsRead()
        {
            foreach (var notification in _notifications)
            {
                notification.IsRead = true;
            }
            SaveToFile();
        }

        public int GetUnreadCount()
        {
            return _notifications.Count(n => !n.IsRead);
        }

        public void Delete(int id)
        {
            var notification = GetById(id);
            if (notification != null)
            {
                _notifications.Remove(notification);
                SaveToFile();
            }
        }

        public void DeleteAll()
        {
            _notifications.Clear();
            SaveToFile();
        }

        private void SaveToFile()
        {
            var json = JsonSerializer.Serialize(_notifications, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }

        private void LoadFromFile()
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var loaded = JsonSerializer.Deserialize<List<Notification>>(json);
                    if (loaded != null)
                        _notifications = loaded;
                }
            }
        }
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].
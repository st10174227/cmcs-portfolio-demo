//using CMCS_POE.Data;
//using Microsoft.AspNetCore.Mvc;

//namespace CMCS_Part2.Controllers
//{
//    public class NotificationsController : Controller
//    {
//        private readonly INotificationRepository _notificationRepository;

//        public NotificationsController(INotificationRepository notificationRepository)
//        {
//            _notificationRepository = notificationRepository;
//        }

//        public IActionResult Index()
//        {
//            var notifications = _notificationRepository.GetAll();
//            ViewBag.UnreadCount = _notificationRepository.GetUnreadCount();
//            return View(notifications);
//        }

//        [HttpPost]
//        public IActionResult MarkAsRead(int id)
//        {
//            _notificationRepository.MarkAsRead(id);
//            return RedirectToAction(nameof(Index));
//        }

//        [HttpPost]
//        public IActionResult MarkAllAsRead()
//        {
//            _notificationRepository.MarkAllAsRead();
//            TempData["Success"] = "All notifications marked as read";
//            return RedirectToAction(nameof(Index));
//        }

//        [HttpPost]
//        public IActionResult Delete(int id)
//        {
//            _notificationRepository.Delete(id);
//            TempData["Success"] = "Notification deleted";
//            return RedirectToAction(nameof(Index));
//        }

//        [HttpPost]
//        public IActionResult DeleteAll()
//        {
//            _notificationRepository.DeleteAll();
//            TempData["Success"] = "All notifications deleted";
//            return RedirectToAction(nameof(Index));
//        }

//        // API endpoint for navbar badge count
//        [HttpGet]
//        public IActionResult GetUnreadCount()
//        {
//            var count = _notificationRepository.GetUnreadCount();
//            return Json(new { count });
//        }
//    }
//}
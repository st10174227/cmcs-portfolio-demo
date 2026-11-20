using CMCS_POE.Models;
using Microsoft.EntityFrameworkCore;

namespace CMCS_POE.Data
{
    public class EFLecturerRepository : ILecturerRepository
    {
        private readonly ApplicationDbContext _context;

        public EFLecturerRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Lecturer> GetAll()
        {
            return _context.Lecturers.Include(l => l.Claims).ToList();
        }

        public Lecturer? GetById(int id)
        {
            return _context.Lecturers.Include(l => l.Claims).FirstOrDefault(l => l.LecturerId == id);
        }

        public void Add(Lecturer lecturer)
        {
            _context.Lecturers.Add(lecturer);
            _context.SaveChanges();
        }

        public void Update(Lecturer lecturer)
        {
            _context.Lecturers.Update(lecturer);
            _context.SaveChanges();
        }

        public void Delete(int id)
        {
            var lecturer = GetById(id);
            if (lecturer != null)
            {
                lecturer.IsActive = false; 
                _context.SaveChanges();
            }
        }

        public Lecturer? GetByEmail(string email)
        {
            return _context.Lecturers.FirstOrDefault(l => l.Email == email);
        }
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].
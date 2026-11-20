using CMCS_POE.Security;
using CMCS_POE.Models;
using CMCS_POE.Data;
using Microsoft.EntityFrameworkCore;

namespace CMCS_POE.Data
{
    public class EFClaimRepository : IClaimRepository
    {
        private readonly ApplicationDbContext _context;

        public EFClaimRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Claim> GetAll()
        {
            return _context.Claims.Include(c => c.Lecturer).ToList();
        }

        public Claim? GetById(int id)
        {
            return _context.Claims.Include(c => c.Lecturer).FirstOrDefault(c => c.ClaimId == id);
        }

        public void Add(Claim claim)
        {
            _context.Claims.Add(claim);
            _context.SaveChanges();
        }

        public void Update(Claim claim)
        {
            _context.Claims.Update(claim);
            _context.SaveChanges();
        }

        public void Delete(int id)
        {
            var claim = GetById(id);
            if (claim != null)
            {
                _context.Claims.Remove(claim);
                _context.SaveChanges();
            }
        }

        public List<Claim> GetApprovedClaims()
        {
            return _context.Claims
                .Include(c => c.Lecturer)
                .Where(c => c.Status == ClaimStages.Approved)
                .ToList();
        }

        public List<Claim> GetPaidClaims()
        {
            return _context.Claims
                .Include(c => c.Lecturer)
                .Where(c => c.IsPaid)
                .ToList();
        }

        public List<Claim> GetUnpaidApprovedClaims()
        {
            return _context.Claims
                .Include(c => c.Lecturer)
                .Where(c => c.Status == ClaimStages.Approved && !c.IsPaid)
                .ToList();
        }

        public List<Claim> GetClaimsByMonth(string month)
        {
            return _context.Claims
                .Include(c => c.Lecturer)
                .Where(c => c.Month == month)
                .ToList();
        }
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].
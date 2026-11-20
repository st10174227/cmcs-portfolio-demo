using CMCS_POE.Models;
using System.Text.Json;

namespace CMCS_POE.Data
{
    public class ClaimRepository : IClaimRepository
    {
        private static List<Claim> _claims = new List<Claim>();
        private readonly string _filePath;

        public ClaimRepository()
        {
            _filePath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "claims.json");
            var directory = Path.GetDirectoryName(_filePath)
                ?? throw new InvalidOperationException("The storage directory could not be resolved.");
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            LoadFromFile();
        }

        public List<Claim> GetAll()
        {
            return _claims;
        }

        public Claim? GetById(int id)
        {
            return _claims.FirstOrDefault(c => c.ClaimId == id);
        }

        public void Add(Claim claim)
        {
            _claims.Add(claim);
            SaveToFile();
        }

        public void Update(Claim claim)
        {
            var existing = GetById(claim.ClaimId);
            if (existing != null)
            {
                existing.Status = claim.Status;
                existing.LecturerName = claim.LecturerName;
                existing.LecturerId = claim.LecturerId;
                existing.Month = claim.Month;
                existing.HoursWorked = claim.HoursWorked;
                existing.HourlyRate = claim.HourlyRate;
                existing.Notes = claim.Notes;
                existing.DocumentName = claim.DocumentName;
                existing.DocumentPath = claim.DocumentPath;
                existing.IsPaid = claim.IsPaid;
                existing.PaidDate = claim.PaidDate;
                existing.PaymentReference = claim.PaymentReference;
                existing.ApprovedDate = claim.ApprovedDate;
                SaveToFile();
            }
        }

        public void Delete(int id)
        {
            var claim = GetById(id);
            if (claim != null)
            {
                _claims.Remove(claim);
                SaveToFile();
            }
        }

        public void Clear()
        {
            _claims.Clear();
            SaveToFile();
        }

        public List<Claim> GetApprovedClaims()
        {
            var result = _claims
                .Where(c => c.Status == "Verified" || c.Status == "Approved")
                .ToList();

            Console.WriteLine($"GetApprovedClaims: Found {result.Count} claims");
            return result;
        }

        public List<Claim> GetPaidClaims()
        {
            var result = _claims
                .Where(c => (c.Status == "Verified" || c.Status == "Approved") && c.IsPaid == true)
                .ToList();

            Console.WriteLine($"GetPaidClaims: Found {result.Count} claims");
            return result;
        }

        public List<Claim> GetUnpaidApprovedClaims()
        {
            var result = _claims
                .Where(c => (c.Status == "Verified" || c.Status == "Approved") && c.IsPaid == false)
                .OrderByDescending(c => c.ClaimId)
                .ToList();

            Console.WriteLine($"GetUnpaidApprovedClaims: Found {result.Count} unpaid claims");
            foreach (var claim in result)
            {
                Console.WriteLine($"  - Claim #{claim.ClaimId}: Status={claim.Status}, IsPaid={claim.IsPaid}");
            }

            return result;
        }

        public List<Claim> GetClaimsByMonth(string month)
        {
            return _claims
                .Where(c => c.Month == month)
                .ToList();
        }

        private void SaveToFile()
        {
            var json = JsonSerializer.Serialize(_claims, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }

        private void LoadFromFile()
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var loadedClaims = JsonSerializer.Deserialize<List<Claim>>(json);
                    if (loadedClaims != null)
                        _claims = loadedClaims;
                }
            }
        }
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].
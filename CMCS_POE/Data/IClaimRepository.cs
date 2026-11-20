using CMCS_POE.Models;

namespace CMCS_POE.Data
{
    public interface IClaimRepository
    {
        List<Claim> GetAll();
        Claim? GetById(int id);
        void Add(Claim claim);
        void Update(Claim claim);
        void Delete(int id);

        List<Claim> GetApprovedClaims();
        List<Claim> GetPaidClaims();
        List<Claim> GetUnpaidApprovedClaims();
        List<Claim> GetClaimsByMonth(string month);

    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].
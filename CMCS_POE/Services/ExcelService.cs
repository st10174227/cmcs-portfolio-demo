using ClosedXML.Excel;
using CMCS_POE.Models;

namespace CMCS_POE.Services
{
    public class ExcelService : IExcelService
    {
        public byte[] ExportClaims(List<Claim> claims)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Claims");

                // Headers
                worksheet.Cell(1, 1).Value = "Claim ID";
                worksheet.Cell(1, 2).Value = "Lecturer";
                worksheet.Cell(1, 3).Value = "Month";
                worksheet.Cell(1, 4).Value = "Hours";
                worksheet.Cell(1, 5).Value = "Rate";
                worksheet.Cell(1, 6).Value = "Total";
                worksheet.Cell(1, 7).Value = "Status";
                worksheet.Cell(1, 8).Value = "Paid";
                worksheet.Cell(1, 9).Value = "Payment Ref";

                // Style headers
                var headerRange = worksheet.Range(1, 1, 1, 9);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;

                // Data
                int row = 2;
                foreach (var claim in claims)
                {
                    worksheet.Cell(row, 1).Value = claim.ClaimId;
                    worksheet.Cell(row, 2).Value = claim.LecturerName;
                    worksheet.Cell(row, 3).Value = claim.Month;
                    worksheet.Cell(row, 4).Value = claim.HoursWorked;
                    worksheet.Cell(row, 5).Value = claim.HourlyRate;
                    worksheet.Cell(row, 6).Value = claim.TotalAmount;
                    worksheet.Cell(row, 7).Value = claim.Status;
                    worksheet.Cell(row, 8).Value = claim.IsPaid ? "Yes" : "No";
                    worksheet.Cell(row, 9).Value = claim.PaymentReference ?? "";
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        public byte[] ExportLecturers(List<Lecturer> lecturers)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Lecturers");

                worksheet.Cell(1, 1).Value = "ID";
                worksheet.Cell(1, 2).Value = "Full Name";
                worksheet.Cell(1, 3).Value = "Email";
                worksheet.Cell(1, 4).Value = "Phone";
                worksheet.Cell(1, 5).Value = "Department";
                worksheet.Cell(1, 6).Value = "Rate";
                worksheet.Cell(1, 7).Value = "Active";

                var headerRange = worksheet.Range(1, 1, 1, 7);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGreen;

                int row = 2;
                foreach (var lecturer in lecturers)
                {
                    worksheet.Cell(row, 1).Value = lecturer.LecturerId;
                    worksheet.Cell(row, 2).Value = lecturer.FullName;
                    worksheet.Cell(row, 3).Value = lecturer.Email;
                    worksheet.Cell(row, 4).Value = lecturer.PhoneNumber;
                    worksheet.Cell(row, 5).Value = lecturer.Department;
                    worksheet.Cell(row, 6).Value = lecturer.StandardHourlyRate;
                    worksheet.Cell(row, 7).Value = lecturer.IsActive ? "Yes" : "No";
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].
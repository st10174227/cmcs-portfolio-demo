using CMCS_POE.Demo;
using CMCS_POE.Presentation;
using CMCS_POE.Models; 
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;

namespace CMCS_POE.Services
{
    public class PdfService : IPdfService
    {
        private readonly bool _demo;
        public PdfService(DemoOptions? demo = null) { _demo = demo?.Enabled == true; }
        public byte[] GenerateInvoice(Claim claim)
        {
            using (var ms = new MemoryStream())
            {
                var document = new Document(PageSize.A4, 50, 50, 25, 25);
                var writer = PdfWriter.GetInstance(document, ms);
                document.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

                document.Add(new Paragraph("CMCS", titleFont) { Alignment = Element.ALIGN_CENTER });
                document.Add(new Paragraph(_demo ? "DEMO CLAIM INVOICE" : "CLAIM INVOICE", headerFont) { Alignment = Element.ALIGN_CENTER });
                document.Add(new Paragraph("\n"));

                document.Add(new Paragraph($"Invoice #: INV-{claim.ClaimId:D6}", headerFont));
                document.Add(new Paragraph($"Issued (UTC): {DateTime.UtcNow:yyyy-MM-dd}", normalFont));
                document.Add(new Paragraph($"Payment Ref: {claim.PaymentReference ?? "N/A"}", normalFont));
                document.Add(new Paragraph("\n"));

                document.Add(new Paragraph("LECTURER / PAYEE:", headerFont));
                document.Add(new Paragraph($"Lecturer: {claim.LecturerName}", normalFont));
                document.Add(new Paragraph($"Month: {claim.Month}", normalFont));
                document.Add(new Paragraph("\n"));

                var table = new PdfPTable(4) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 3, 2, 2, 2 });

                AddCell(table, "Description", headerFont);
                AddCell(table, "Hours", headerFont);
                AddCell(table, "Rate", headerFont);
                AddCell(table, "Amount", headerFont);

                AddCell(table, "Lecturing Services", normalFont);
                AddCell(table, claim.HoursWorked.ToString("F2"), normalFont);
                AddCell(table, Money.Zar(claim.HourlyRate), normalFont);
                AddCell(table, Money.Zar(claim.TotalAmount), normalFont);

                document.Add(table);
                document.Add(new Paragraph("\n"));

                var totalTable = new PdfPTable(2) { WidthPercentage = 50, HorizontalAlignment = Element.ALIGN_RIGHT };
                AddCell(totalTable, "TOTAL:", headerFont);
                AddCell(totalTable, Money.Zar(claim.TotalAmount), headerFont);
                document.Add(totalTable);

                document.Add(new Paragraph("\n"));
                document.Add(new Paragraph($"Claim status: {claim.Status}", normalFont));
                document.Add(new Paragraph($"Payment status: {(claim.IsPaid ? "PAID (recorded)" : "UNPAID")}", headerFont));
                if (claim.IsPaid && claim.PaidDate.HasValue)
                {
                    document.Add(new Paragraph($"Payment recorded (UTC): {claim.PaidDate:yyyy-MM-dd}", normalFont));
                }

                document.Add(new Paragraph("\n"));
                document.Add(new Paragraph(_demo ? "Portfolio demo - fictional data. No money is transferred." : "This invoice records a claim. Payment status does not initiate a bank transfer.", normalFont));
                document.Close();
                return ms.ToArray();
            }
        }
        public byte[] GenerateMonthlyReport(List<Claim> claims, string month)
        {
            using (var ms = new MemoryStream())
            {
                var document = new Document(PageSize.A4.Rotate(), 50, 50, 25, 25);
                var writer = PdfWriter.GetInstance(document, ms);
                document.Open();

                var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18);
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10);
                var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 9);

                document.Add(new Paragraph($"{(_demo ? "DEMO MONTHLY CLAIM REPORT" : "MONTHLY CLAIM REPORT")} - {month}", titleFont) { Alignment = Element.ALIGN_CENTER });
                document.Add(new Paragraph($"Generated (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm}", normalFont) { Alignment = Element.ALIGN_CENTER });
                document.Add(new Paragraph("\n"));

                var table = new PdfPTable(7) { WidthPercentage = 100 };
                table.SetWidths(new float[] { 1, 3, 2, 2, 2, 2, 2 });

                AddCell(table, "ID", headerFont);
                AddCell(table, "Lecturer", headerFont);
                AddCell(table, "Hours", headerFont);
                AddCell(table, "Rate", headerFont);
                AddCell(table, "Total", headerFont);
                AddCell(table, "Status", headerFont);
                AddCell(table, "Paid", headerFont);

                foreach (var claim in claims)
                {
                    AddCell(table, claim.ClaimId.ToString(), normalFont);
                    AddCell(table, claim.LecturerName, normalFont);
                    AddCell(table, claim.HoursWorked.ToString("F2"), normalFont);
                    AddCell(table, Money.Zar(claim.HourlyRate), normalFont);
                    AddCell(table, Money.Zar(claim.TotalAmount), normalFont);
                    AddCell(table, claim.Status, normalFont);
                    AddCell(table, claim.IsPaid ? "Yes" : "No", normalFont);
                }

                document.Add(table);
                document.Add(new Paragraph("\n"));

                var totalAmount = claims.Sum(c => c.TotalAmount);
                var paidAmount = claims.Where(c => c.IsPaid).Sum(c => c.TotalAmount);
                document.Add(new Paragraph($"Total Claims: {claims.Count}", headerFont));
                document.Add(new Paragraph($"Total Amount: R {totalAmount:N2}", headerFont));
                document.Add(new Paragraph($"Paid Amount: R {paidAmount:N2}", headerFont));
                document.Add(new Paragraph($"Outstanding: R {(totalAmount - paidAmount):N2}", headerFont));

                document.Close();
                return ms.ToArray();
            }
        }

        private void AddCell(PdfPTable table, string? text, Font font)
        {
            var cell = new PdfPCell(new Phrase(text ?? string.Empty, font));
            cell.Padding = 5;
            table.AddCell(cell);
        }
    }
}
//OpenAI.2025.Chat-GPT(Version 5).[Large language model]Available at: https://chatgpt.com/c/68b46a0b-99f8-8326-98b4-5245e5c93d35[Accessed: 20 November 2025].
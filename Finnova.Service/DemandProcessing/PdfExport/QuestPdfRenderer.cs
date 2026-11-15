using Finnova.Models.Contracts.DemandProcessing;
using Finnova.Service.DemandProcessing.Ports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Finnova.Service.DemandProcessing.PdfExport;

public sealed class QuestPdfRenderer : IPdfRenderer
{
    public byte[] Render(DemandPdfDocument document)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return QuestPDF.Fluent.Document.Create(container => container.Page(page => { page.Margin(24); page.Header().Text(document.Title).FontSize(18).Bold(); page.Content().Column(column => { column.Item().Text($"Generated at UTC: {document.GeneratedAtUtc:O}"); column.Item().Text($"Run: {document.Run.RunId:N}, Revision: {document.Run.RevisionNumber}"); column.Item().Text($"Period: {document.Run.PeriodYear:D4}-{document.Run.PeriodMonth:D2}, LOB: {document.Run.LineOfBusinessId:N}"); column.Item().Text($"Total Demand: INR {document.TotalDemand:0.00}"); foreach (var row in document.Rows) column.Item().Text($"Account {row.AccountId:N} | Branch {row.BranchId:N} | Current Due INR {row.CurrentDue:0.00} | Arrears INR {row.Arrears:0.00} | ODI INR {row.Odi:0.00} | Other Charges INR {row.OtherCharges:0.00} | Total Demand INR {row.TotalDemand:0.00}"); foreach (var batch in document.Batches) column.Item().Text($"Branch {batch.BranchId:N} | Status {batch.Status} | Rows {batch.RowCount} | Total Demand INR {batch.TotalDemand:0.00}"); }); page.Footer().AlignRight().Text("Finnova Demand Processing"); })).GeneratePdf();
    }
}

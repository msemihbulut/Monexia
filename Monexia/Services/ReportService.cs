using ClosedXML.Excel;
using Microsoft.AspNetCore.Hosting;
using Monexia.Helpers;
using Monexia.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using QuestPDF.Infrastructure;

namespace Monexia.Services
{
    public class ReportService : IReportService
    {
        private readonly IWebHostEnvironment _hostEnvironment;

        public ReportService(IWebHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public Task<byte[]> CreateExcelReportAsync(IEnumerable<Transaction> transactions)
        {
            var reportDate = DateTime.Now;
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Finansal Rapor");

                var logoPath = Path.Combine(_hostEnvironment.WebRootPath, "img", "monexia-logo.png");
                if (System.IO.File.Exists(logoPath))
                {
                    worksheet.AddPicture(logoPath)
                        .MoveTo(worksheet.Cell("A1"))
                        .Scale(0.25);
                }

                worksheet.Cell("C2").Value = "Monexia Finansal Raporu";
                worksheet.Cell("C2").Style.Font.Bold = true;
                worksheet.Cell("C2").Style.Font.FontSize = 18;
                worksheet.Range("C2:F2").Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                worksheet.Cell("C3").Value = $"Rapor Tarihi: {reportDate:dd.MM.yyyy}";
                worksheet.Cell("C3").Style.Font.Italic = true;
                worksheet.Range("C3:F3").Merge().Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

                var headerRow = 6;
                worksheet.Cell(headerRow, 1).Value = "Tarih";
                worksheet.Cell(headerRow, 2).Value = "Açıklama";
                worksheet.Cell(headerRow, 3).Value = "Kategori";
                worksheet.Cell(headerRow, 4).Value = "Tür";
                worksheet.Cell(headerRow, 5).Value = "Tutar";

                var headerRange = worksheet.Range(headerRow, 1, headerRow, 5);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#764ABC");
                headerRange.Style.Font.FontColor = XLColor.White;

                int currentRow = headerRow + 1;
                foreach (var t in transactions)
                {
                    var categoryReadable = CommonHelper.ToHumanReadable(t.Type == TransactionType.Income ? t.IncomeCategory.ToString() : t.ExpenseCategory.ToString());
                    var typeReadable = t.Type == TransactionType.Income ? "Gelir" : "Gider";

                    worksheet.Cell(currentRow, 1).Value = t.TransactionDate.ToString("dd.MM.yyyy");
                    worksheet.Cell(currentRow, 2).Value = t.Description;
                    worksheet.Cell(currentRow, 3).Value = categoryReadable;
                    worksheet.Cell(currentRow, 4).Value = typeReadable;
                    worksheet.Cell(currentRow, 5).Value = t.Amount;
                    worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "₺#,##0.00";

                    if (t.Type == TransactionType.Income)
                        worksheet.Cell(currentRow, 5).Style.Font.FontColor = XLColor.FromHtml("#2E7D32");
                    else
                        worksheet.Cell(currentRow, 5).Style.Font.FontColor = XLColor.FromHtml("#C62828");

                    currentRow++;
                }

                worksheet.Columns().AdjustToContents();
                worksheet.Column(5).Width = 15;

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return Task.FromResult(stream.ToArray());
                }
            }
        }

        public async Task<byte[]> CreatePdfReportAsync(IEnumerable<Transaction> transactions, ApplicationUser user)
        {
            var reportDate = DateTime.Now;
            var logoPath = Path.Combine(_hostEnvironment.WebRootPath, "img", "monexia-logo.png");
            var logoBytes = System.IO.File.Exists(logoPath) ? await System.IO.File.ReadAllBytesAsync(logoPath) : null;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(Fonts.Arial));

                    page.Header()
                        .Row(row =>
                        {
                            if (logoBytes != null)
                            {
                                row.ConstantItem(100).Image(logoBytes);
                            }

                            row.RelativeItem().Column(col =>
                            {
                                col.Item().AlignCenter().Text("Monexia Finansal Rapor").SemiBold().FontSize(20).FontColor(Colors.Purple.Medium);
                                col.Item().AlignCenter().Text($"Rapor Oluşturma Tarihi: {reportDate:dd MMMM yyyy}").FontSize(9).Light();
                                col.Item().AlignCenter().Text($"Kullanıcı: {user.Name} {user.Surname}").FontSize(9).Light();
                            });
                            row.ConstantItem(100);
                        });

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(col =>
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(3);
                                    columns.RelativeColumn(2);
                                    columns.RelativeColumn(1.5f);
                                    columns.RelativeColumn(2);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Purple.Medium).Padding(5).Text("Tarih").FontColor(Colors.White);
                                    header.Cell().Background(Colors.Purple.Medium).Padding(5).Text("Açıklama").FontColor(Colors.White);
                                    header.Cell().Background(Colors.Purple.Medium).Padding(5).Text("Kategori").FontColor(Colors.White);
                                    header.Cell().Background(Colors.Purple.Medium).Padding(5).Text("Tür").FontColor(Colors.White);
                                    header.Cell().Background(Colors.Purple.Medium).Padding(5).Text("Tutar").FontColor(Colors.White);
                                });

                                foreach (var t in transactions)
                                {
                                    var categoryReadable = CommonHelper.ToHumanReadable(t.Type == TransactionType.Income ? t.IncomeCategory.ToString() : t.ExpenseCategory.ToString());
                                    var typeReadable = t.Type == TransactionType.Income ? "Gelir" : "Gider";

                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(t.TransactionDate.ToString("dd.MM.yyyy"));
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(t.Description);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(categoryReadable);
                                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(typeReadable);
                                    var amountCell = table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);
                                    if (t.Type == TransactionType.Income)
                                        amountCell.Text($"+{t.Amount:N2} ₺").FontColor(Colors.Green.Medium).SemiBold();
                                    else
                                        amountCell.Text($"-{t.Amount:N2} ₺").FontColor(Colors.Red.Medium).SemiBold();
                                }
                            });
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Sayfa ");
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                        });
                });
            }).GeneratePdf();

            return document;
        }
    }
}
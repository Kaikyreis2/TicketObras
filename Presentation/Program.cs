using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Presentation;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapGet("excell", ([FromBody] List<Ticket> tickets) =>
{

    using var workbook = new XLWorkbook();
    var worksheet = workbook.Worksheets.Add("Tickets");

   
    var headers = new[]
    {
                "CEP", "Cidade", "Bairro", "Rua",
                "Contribuinte", "Telefone", "Data do Pedido", "Status do Pedido", "OS"
            };

    for (int i = 0; i < headers.Length; i++)
    {
        worksheet.Cell(1, i + 1).Value = headers[i];
        worksheet.Cell(1, i + 1).Style.Font.Bold = true;
    }

   
    for (int i = 0; i < tickets.Count; i++)
    {
        var row = i + 2;
        var t = tickets[i];
        worksheet.Cell(row, 1).Value = t.CEP;
        worksheet.Cell(row, 2).Value = t.Cidade;
        worksheet.Cell(row, 3).Value = t.Bairro;
        worksheet.Cell(row, 4).Value = t.Rua;
        worksheet.Cell(row, 6).Value = t.Contribuinte;
        worksheet.Cell(row, 7).Value = t.Telefone;
        worksheet.Cell(row, 8).Value = t.DataDoPedido;
        worksheet.Cell(row, 9).Value = t.StatusDoPedido;
        worksheet.Cell(row, 10).Value = t.OS;
    }

    worksheet.Columns().AdjustToContents();

    using var stream = new MemoryStream();
    workbook.SaveAs(stream);
    var content = stream.ToArray();

    return Results.File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "tickets.xlsx");
});

app.MapGet("/pdf", ([FromBody] List<Ticket> tickets) =>
{
    QuestPDF.Settings.License = LicenseType.Community;

    using var stream = new MemoryStream();
    var document = Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(9));

            page.Header().Text("Relatório de Tickets").SemiBold().FontSize(16).AlignCenter();

            page.Content().Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    foreach (var _ in Enumerable.Range(0, 10)) cols.RelativeColumn();
                });

                var headers = new[] { "CEP", "Cidade", "Bairro", "Rua", "Localidade", "Contribuinte", "Telefone", "Data Pedido", "Status", "OS" };

                foreach (var h in headers)
                    table.Cell().Element(PdfCellStyle).Background("#e0e0e0").Text(h).Bold();

                foreach (var t in tickets)
                {
                    table.Cell().Element(PdfCellStyle).Text(t.CEP);
                    table.Cell().Element(PdfCellStyle).Text(t.Cidade);
                    table.Cell().Element(PdfCellStyle).Text(t.Bairro);
                    table.Cell().Element(PdfCellStyle).Text(t.Rua);
                    table.Cell().Element(PdfCellStyle).Text(t.Contribuinte);
                    table.Cell().Element(PdfCellStyle).Text(t.Telefone);
                    table.Cell().Element(PdfCellStyle).Text(t.DataDoPedido);
                    table.Cell().Element(PdfCellStyle).Text(t.StatusDoPedido);
                    table.Cell().Element(PdfCellStyle).Text(t.OS);
                }
            });
        });
    });

    document.GeneratePdf(stream);
    return Results.File(stream.ToArray(), "application/pdf", "tickets.pdf");
});

static IContainer PdfCellStyle(IContainer container)
    => container.Border(0.5f).PaddingVertical(2).PaddingHorizontal(3);

app.Run();

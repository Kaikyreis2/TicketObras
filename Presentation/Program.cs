using ClosedXML.Excel;
using static BCrypt.Net.BCrypt;
using Infrastructure;
using Domain;
using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;
using System.Configuration;
using Application;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Presentation;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;




var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(op =>
{
    op.AddPolicy("AngularDev", po =>
    {
        po.WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

builder.Services.AddDbContext<Context>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"), e => e.CommandTimeout(120)));
builder.Services.AddAuthentication("Cookies").AddCookie("Cookies",c =>
{
    c.ExpireTimeSpan = TimeSpan.FromDays(1);
    c.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    c.Cookie.SameSite = SameSiteMode.Strict;
    c.Cookie.HttpOnly = true;
});
builder.Services.AddAuthorization();

builder.Services.AddTransient<ITicketRepository, TicketRepository>();
builder.Services.AddTransient<UserRepository>();
builder.Services.AddTransient<RoleRepository>();
builder.Services.AddTransient<AccountService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();


app.UseCors("AngularDev");

app.UseRouting();

app.Use(async (context, next) =>
{
    var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();

    var validApiKey = builder.Configuration.GetValue<string>("api-key-x");

    if (string.IsNullOrEmpty(validApiKey))
    {
        await next();
        return;
    }

    if (string.IsNullOrEmpty(apiKey) || !apiKey.Equals(validApiKey, StringComparison.Ordinal))
    {
        context.Response.StatusCode = 401;
        return;
    }

    await next();
}).UseAuthentication().UseAuthorization();

var prefix = app.MapGroup("/api/v1");

prefix.MapGet("/", () => "Hello World!").RequireAuthorization();

prefix.MapGet("/tickets", async ([FromServices] ITicketRepository _repository) =>
{
    var result = await _repository.GetAllAsync();
    
    return Results.Ok(result);
}).RequireAuthorization();

prefix.MapPut("/tickets", async ([FromServices] ITicketRepository _repository, [FromBody] Ticket ticket) =>
{

    return Results.Ok(await _repository.UpdateAsync(ticket));
}).RequireAuthorization();

prefix.MapPost("/tickets", async ([FromServices] ITicketRepository _repository, [FromBody] Ticket ticket) =>
{

    return Results.Ok(await _repository.AddAsync(ticket));
}).RequireAuthorization();

prefix.MapPost("/login",[AllowAnonymous] async ([FromBody] UserRequest request, [FromServices] AccountService accountService,  HttpContext context) =>
{
    try
    {
        var isValidUser = await accountService.CheckLoginAsync(request.Email, request.Password);

        if (isValidUser is null)
            return Results.BadRequest("Login invalido");


        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Email, isValidUser.Email)
        };


        foreach (var role in isValidUser.Roles)
        {
            claims.Append(new Claim(ClaimTypes.Role, role.Name));
        }

        var claimIdentity = new ClaimsIdentity(claims, "Cookies");

        await context.SignInAsync("Cookies", new ClaimsPrincipal(claimIdentity));

       return Results.Ok();

    }catch(Exception e)
    {
        return Results.InternalServerError(e.Message);
    }
});

prefix.MapPost("/logout", async ([FromServices] AccountService accountService, HttpContext context) =>
{
    try
    {
        await context.SignOutAsync("Cookies");
        return Results.Ok();

    }
    catch (Exception e)
    {
        return Results.InternalServerError(e.Message);
    }
}).RequireAuthorization();

/*prefix.MapPost("/register", [AllowAnonymous] async ([FromServices] UserRepository repository, [FromBody] UserRequest request) =>
{
    try
    {

        var passwordHash = HashPassword(request.Password);
        await repository.Add(new User() { Email = request.Email, PasswordHash = passwordHash});
        return Results.Ok(passwordHash);

    }
    catch (Exception e)
    {
        return Results.InternalServerError(e.Message);
    }
});*/

/*prefix.MapGet("/hello", ([FromServices] Context context) =>
{
    return Results.Ok(context);
});

prefix.MapGet("/excell", async  ([FromServices] ITicketRepository _repository) =>
{
    var tickets = await _repository.GetAllAsync();
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

    return Results.File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"tickets{DateTime.UtcNow::g}.xlsx");
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
*/
/*
static IContainer PdfCellStyle(IContainer container)
    => container.Border(0.5f).PaddingVertical(2).PaddingHorizontal(3);
*/
app.Run();

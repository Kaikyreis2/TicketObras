using Application;
using ClosedXML.Excel;
using Domain;
using Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Presentation;
using System.Buffers.Text;
using System.Configuration;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static BCrypt.Net.BCrypt;




var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(op =>
{
    op.AddPolicy("AngularDev", po =>
    {
        po.WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();

        po.WithOrigins("https://ticket-green.vercel.app")
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
    c.Cookie.SameSite = SameSiteMode.None;
    c.Cookie.HttpOnly = true;
    c.Events = new CookieAuthenticationEvents()
    {
        OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        },

        OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = 401;
            return Task.CompletedTask;
        }


    };
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Admin", p => p.RequireRole("Admin"))
    .AddPolicy("User", p => p.RequireRole("User"))
    .AddPolicy("Moderator", p => p.RequireRole("Moderator"))
    .AddPolicy("ReadOnly", p => p.RequireRole("ReadOnly"));

builder.Services.AddTransient<ITicketRepository, TicketRepository>();
builder.Services.AddTransient<IUserRepository, UserRepository>();
builder.Services.AddTransient<IRoleRepository, RoleRepository>();
builder.Services.AddTransient<AccountService>();
builder.Services.AddTransient<HttpClient>();

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});
var app = builder.Build();

app.UseRouting();

/*app.Use(async (context, next) =>
{
    var iplocal = context.Connection.LocalIpAddress?.ToString();

    if(string.IsNullOrEmpty(iplocal))
        iplocal = context.Request.Headers["X-Forwarded-For"].ToString();
    

    List<string> ipsAllowed = builder.Configuration.GetValue<List<string>>("ips-allowed") ?? [];

    if (string.IsNullOrEmpty(iplocal) || !ipsAllowed.Contains(iplocal))
    {
        context.Response.StatusCode = 403;
        return;
    }

    await next();

});*/
app.UseCors("AngularDev");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();


var prefix = app.MapGroup("/api/v1");



prefix.MapGet("/health", [AllowAnonymous] () => { return Results.Ok("Running"); });


prefix.MapGet("/tickets", async ([FromServices] ITicketRepository _repository) =>
{
    var result = await _repository.GetAllAsync();
    
    return Results.Ok(result);
}).RequireAuthorization("User");

prefix.MapPut("/tickets", async ([FromServices] ITicketRepository _repository, [FromBody] Ticket ticket) =>
{

    return Results.Ok(await _repository.UpdateAsync(ticket));
}).RequireAuthorization("Admin", "User");

prefix.MapPost("/tickets", async ([FromServices] ITicketRepository _repository, [FromBody] Ticket ticket) =>
{
    return Results.Ok(await _repository.AddAsync(ticket));
}).RequireAuthorization("Admin","User");

prefix.MapDelete("/tickets/{Id:int}", async ([FromServices] ITicketRepository _repository, [FromRoute] int id) =>
{
    return Results.Ok(await _repository.DeleteAsync(id));
}).RequireAuthorization("Admin", "User");



prefix.MapPost("/login",[AllowAnonymous] async ([FromBody] UserRequest request, [FromServices] AccountService accountService,  HttpContext context) =>
{
    try
    {
        var isValidUser = await accountService.CheckLoginAsync(request.Email, request.Password);
        if (isValidUser is null)
            return Results.BadRequest("Login inválido");

        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Email, isValidUser.Email)
    };
        foreach (var role in isValidUser.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Name));
        }

        var claimIdentity = new ClaimsIdentity(claims, "Cookies");

        await context.SignInAsync("Cookies", new ClaimsPrincipal(claimIdentity));
        var stringBuilder = new StringBuilder();
        return Results.Ok();
    }
    catch (Exception e)
    {
        return Results.Problem(e.Message);
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

prefix.MapPost("/register", async ([FromServices] IUserRepository repository, [FromBody] UserRequest request) =>
{
    try
    {

        var passwordHash = HashPassword(request.Password);
        await repository.AddAsync(new User() { Email = request.Email, PasswordHash = passwordHash});
        return Results.Ok(passwordHash);

    }
    catch (Exception e)
    {
        return Results.InternalServerError(e.Message);
    }
}).RequireAuthorization("Admin");


prefix.MapGet("/users", async ([FromServices] IUserRepository _repository) =>
{
    return Results.Ok(await _repository.GetAllAsync());
}).RequireAuthorization("Admin");

prefix.MapGet("/users/{id:int}", async ([FromServices] IUserRepository _repository, [FromRoute] int Id) =>
{
    var result = await _repository.GetByIdAsync(Id);

    if (result == null)
        return Results.NotFound();

    return Results.Ok(result);
}).RequireAuthorization("Admin");
prefix.MapGet("/users/current", (HttpContext context) =>
{
    var user = context.User;
    var email = context.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
    var roles = context.User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();
    return Results.Ok(new
    {
        email = email,
        roles = roles 
    });
}).RequireAuthorization("Admin");
prefix.MapPut("/users", async ([FromServices] IUserRepository _repository, [FromBody] User user) =>
{
    return Results.Ok(await _repository.UpdateAsync(user));
}).RequireAuthorization("Admin");
prefix.MapDelete("/users/{id:int}", async ([FromServices] IUserRepository _repository, [FromRoute] int Id) =>
{
    return Results.Ok(await _repository.RemoveAsync(new User() { Id = Id}));
}).RequireAuthorization("Admin");



prefix.MapGet("/roles", async ([FromServices] IRoleRepository _repository) =>
{
    return Results.Ok(await _repository.GetAllAsync());
}).RequireAuthorization("Admin");
prefix.MapGet("/roles/{id:int}", async ([FromServices] IRoleRepository _repository, [FromRoute] int Id) =>
{
    return Results.Ok(await _repository.GetByIdAsync(Id));
}).RequireAuthorization("Admin");
prefix.MapDelete("/roles/{id:int}", async ([FromServices] IRoleRepository _repository, [FromRoute] int Id) =>
{
    return Results.Ok(await _repository.DeleteAsync(new Role() { Id = Id }));
}).RequireAuthorization("Admin");
prefix.MapPut("/roles", async ([FromServices] IRoleRepository _repository, [FromBody] Role role) =>
{
    return Results.Ok(await _repository.UpdateAsync(role));
}).RequireAuthorization("Admin");
prefix.MapPost("/roles", async ([FromServices] IRoleRepository _repository, [FromBody] Role role) =>
{
    return Results.Ok(await _repository.AddAsync(role));
}).RequireAuthorization("Admin");



prefix.MapPost("user/{userId:int}/roles/{roleId:int}", async ([FromServices] IUserRepository _repository, [FromServices] IRoleRepository _roleRepository, [FromRoute] int userId, [FromRoute] int roleId) =>
{
    var user = await _repository.GetByIdAsync(userId);
    var role = await _roleRepository.GetByIdAsync(roleId);

    if (user == null)
        return Results.NotFound("User not exist");

    if (role == null)
        return Results.NotFound("Role not exist");

    user.Roles.Add(role);

    return Results.Ok(await _repository.UpdateAsync(user));
}).RequireAuthorization("Admin");

prefix.MapGet("user/{id:int}/roles", async ([FromServices] IRoleRepository _repository) =>
{
    return Results.Ok(await _repository.GetAllAsync());
}).RequireAuthorization("Admin");


prefix.MapPost("email", async ([FromServices] HttpClient client, [FromBody] EmailRequest request) =>
{
    
    var brevoEndpoint = "https://api.brevo.com/v3/smtp/email";
    var body = new
    {
        sender = new
        {
            name = "Relatorio Ticket",
            email = "kaikyreis123@gmail.com"
        },
        to = new[]
    {
        new {
            email = "kaikyreis123@gmail.com",
            name = "kaiky"
        }
    },
        subject = "Relatorio",
        htmlContent = "Teste envio ticket",
        attachment = new[]
        {
            new {
                name = "relatorio.pdf",
                content = "JVBERi0xLjMKJbrfrOAKMyAwIG9iago8PC9UeXBlIC9QYWdlCi9QYXJlbnQgMSAwIFIKL1Jlc291cmNlcyAyIDAgUgovTWVkaWFCb3ggWzAgMCA1OTUuMjc5OTk5OTk5OTk5OTcyNyA4NDEuODg5OTk5OTk5OTk5OTg2NF0KL0NvbnRlbnRzIDQgMCBSCj4+CmVuZG9iago0IDAgb2JqCjw8Ci9MZW5ndGggOTczMQo+PgpzdHJlYW0KMC41NjcwMDAwMDAwMDAwMDAxIHcKMCBHCkJUCi9GMSAxNiBUZgoxOC4zOTk5OTk5OTk5OTk5OTg2IFRMCjAgZwozOS42ODUwMzkzNzAwNzg3NDA3IDc4NS4xOTcwODY2MTQxNzMyNTg2IFRkCihMaXN0YSBkZSBUaWNrZXRzKSBUagpFVAowLiBHCjAuNTY3MDAwMDAwMDAwMDAwMSB3CjAuIEcKMC41NjcwMDAwMDAwMDAwMDAxIHcKMC4xIDAuNzQgMC42MSByZwowLjc4IEcKMC4gdwowLjEgMC43NCAwLjYxIHJnCjQwLiA3NTYuODUwNjI5OTIxMjU5ODM3OCA5Ny40MDU4NzA5MzA4OTY1ODMxIC0yMS40OTk5OTk5OTk5OTk5OTY0IHJlCmYKQlQKL0YyIDEwIFRmCjExLjUgVEwKMS4gZwo0NS4gNzQzLjM1MDYyOTkyMTI1OTgzNzggVGQKKG9zKSBUagpFVAowLjEgMC43NCAwLjYxIHJnCjAuNzggRwowLiB3CjAuMSAwLjc0IDAuNjEgcmcKMTM3LjQwNTg3MDkzMDg5NjU4MzEgNzU2Ljg1MDYyOTkyMTI1OTgzNzggMTAyLjQwODU4OTM3NzQ5ODU1NzggLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKZgpCVAovRjIgMTAgVGYKMTEuNSBUTAoxLiBnCjE0Mi40MDU4NzA5MzA4OTY1NTQ3IDc0My4zNTA2Mjk5MjEyNTk4Mzc4IFRkCihjb250cmlidWludGUpIFRqCkVUCjAuMSAwLjc0IDAuNjEgcmcKMC43OCBHCjAuIHcKMC4xIDAuNzQgMC42MSByZwoyMzkuODE0NDYwMzA4Mzk1MTI2NyA3NTYuODUwNjI5OTIxMjU5ODM3OCAxNjQuMjA2ODc2MDcwODE2NjkwNiAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpmCkJUCi9GMiAxMCBUZgoxMS41IFRMCjEuIGcKMjQ0LjgxNDQ2MDMwODM5NTA5ODMgNzQzLjM1MDYyOTkyMTI1OTgzNzggVGQKKGNpZGFkZSkgVGoKRVQKMC4xIDAuNzQgMC42MSByZwowLjc4IEcKMC4gdwowLjEgMC43NCAwLjYxIHJnCjQwNC4wMjEzMzYzNzkyMTE3ODg5IDc1Ni44NTA2Mjk5MjEyNTk4Mzc4IDkzLjU4MDI2MjcwNzAyNDU1OTIgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKZgpCVAovRjIgMTAgVGYKMTEuNSBUTAoxLiBnCjQwOS4wMjEzMzYzNzkyMTE3ODg5IDc0My4zNTA2Mjk5MjEyNTk4Mzc4IFRkCihkYXRhUGVkaWRvKSBUagpFVAowLjEgMC43NCAwLjYxIHJnCjAuNzggRwowLiB3CjAuMSAwLjc0IDAuNjEgcmcKNDk3LjYwMTU5OTA4NjIzNjM3NjUgNzU2Ljg1MDYyOTkyMTI1OTgzNzggNTcuNjc4NDAwOTEzNzYzNTQ2NSAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpmCkJUCi9GMiAxMCBUZgoxMS41IFRMCjEuIGcKNTAyLjYwMTU5OTA4NjIzNjMxOTYgNzQzLjM1MDYyOTkyMTI1OTgzNzggVGQKKHN0YXR1cykgVGoKRVQKMC4gRwowLjU2NzAwMDAwMDAwMDAwMDEgdwoxLiBnCjAuNzggRwowLjI4MzQ2NDU2NjkyOTEzMzkgdwoxLiBnCjQwLiA3MzUuMzUwNjI5OTIxMjU5NzI0MSA5Ny40MDU4NzA5MzA4OTY1ODMxIC0yMS40OTk5OTk5OTk5OTk5OTY0IHJlCkIKQlQKL0YxIDEwIFRmCjExLjUgVEwKMC4zMTQgZwo0NS4gNzIxLjg1MDYyOTkyMTI1OTgzNzggVGQKKE9TLTAwMTIzNCkgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwoxMzcuNDA1ODcwOTMwODk2NTgzMSA3MzUuMzUwNjI5OTIxMjU5NzI0MSAxMDIuNDA4NTg5Mzc3NDk4NTU3OCAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpCCkJUCi9GMSAxMCBUZgoxMS41IFRMCjAuMzE0IGcKMTQyLjQwNTg3MDkzMDg5NjU1NDcgNzIxLjg1MDYyOTkyMTI1OTgzNzggVGQKKEpv428gZGEgU2lsdmEpIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKMjM5LjgxNDQ2MDMwODM5NTEyNjcgNzM1LjM1MDYyOTkyMTI1OTcyNDEgMTY0LjIwNjg3NjA3MDgxNjY5MDYgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjI0NC44MTQ0NjAzMDgzOTUwOTgzIDcyMS44NTA2Mjk5MjEyNTk4Mzc4IFRkCihT428gUGF1bG8pIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKNDA0LjAyMTMzNjM3OTIxMTc4ODkgNzM1LjM1MDYyOTkyMTI1OTcyNDEgOTMuNTgwMjYyNzA3MDI0NTU5MiAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpCCkJUCi9GMSAxMCBUZgoxMS41IFRMCjAuMzE0IGcKNDA5LjAyMTMzNjM3OTIxMTc4ODkgNzIxLjg1MDYyOTkyMTI1OTgzNzggVGQKKCkgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwo0OTcuNjAxNTk5MDg2MjM2Mzc2NSA3MzUuMzUwNjI5OTIxMjU5NzI0MSA1Ny42Nzg0MDA5MTM3NjM1NDY1IC0yMS40OTk5OTk5OTk5OTk5OTY0IHJlCkIKQlQKL0YxIDEwIFRmCjExLjUgVEwKMC4zMTQgZwo1MDIuNjAxNTk5MDg2MjM2MzE5NiA3MjEuODUwNjI5OTIxMjU5ODM3OCBUZAooKSBUagpFVAoxLiBnCjAuNzggRwowLjI4MzQ2NDU2NjkyOTEzMzkgdwoxLiBnCjQwLiA3MTMuODUwNjI5OTIxMjU5ODM3OCA5Ny40MDU4NzA5MzA4OTY1ODMxIC0yMS40OTk5OTk5OTk5OTk5OTY0IHJlCkIKQlQKL0YxIDEwIFRmCjExLjUgVEwKMC4zMTQgZwo0NS4gNzAwLjM1MDYyOTkyMTI1OTgzNzggVGQKKE9TLTAwMDEyMzQpIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKMTM3LjQwNTg3MDkzMDg5NjU4MzEgNzEzLjg1MDYyOTkyMTI1OTgzNzggMTAyLjQwODU4OTM3NzQ5ODU1NzggLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjE0Mi40MDU4NzA5MzA4OTY1NTQ3IDcwMC4zNTA2Mjk5MjEyNTk4Mzc4IFRkCihKb+NvIGRhIFNpbHZhKSBUagpFVAoxLiBnCjAuNzggRwowLjI4MzQ2NDU2NjkyOTEzMzkgdwoxLiBnCjIzOS44MTQ0NjAzMDgzOTUxMjY3IDcxMy44NTA2Mjk5MjEyNTk4Mzc4IDE2NC4yMDY4NzYwNzA4MTY2OTA2IC0yMS40OTk5OTk5OTk5OTk5OTY0IHJlCkIKQlQKL0YxIDEwIFRmCjExLjUgVEwKMC4zMTQgZwoyNDQuODE0NDYwMzA4Mzk1MDk4MyA3MDAuMzUwNjI5OTIxMjU5ODM3OCBUZAooU+NvIFBhdWxvbykgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwo0MDQuMDIxMzM2Mzc5MjExNzg4OSA3MTMuODUwNjI5OTIxMjU5ODM3OCA5My41ODAyNjI3MDcwMjQ1NTkyIC0yMS40OTk5OTk5OTk5OTk5OTY0IHJlCkIKQlQKL0YxIDEwIFRmCjExLjUgVEwKMC4zMTQgZwo0MDkuMDIxMzM2Mzc5MjExNzg4OSA3MDAuMzUwNjI5OTIxMjU5ODM3OCBUZAooKSBUagpFVAoxLiBnCjAuNzggRwowLjI4MzQ2NDU2NjkyOTEzMzkgdwoxLiBnCjQ5Ny42MDE1OTkwODYyMzYzNzY1IDcxMy44NTA2Mjk5MjEyNTk4Mzc4IDU3LjY3ODQwMDkxMzc2MzU0NjUgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjUwMi42MDE1OTkwODYyMzYzMTk2IDcwMC4zNTA2Mjk5MjEyNTk4Mzc4IFRkCigpIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKNDAuIDY5Mi4zNTA2Mjk5MjEyNTk4Mzc4IDk3LjQwNTg3MDkzMDg5NjU4MzEgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjQ1LiA2NzguODUwNjI5OTIxMjU5ODM3OCBUZAooT1MtMDAwMTI3NykgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwoxMzcuNDA1ODcwOTMwODk2NTgzMSA2OTIuMzUwNjI5OTIxMjU5ODM3OCAxMDIuNDA4NTg5Mzc3NDk4NTU3OCAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpCCkJUCi9GMSAxMCBUZgoxMS41IFRMCjAuMzE0IGcKMTQyLjQwNTg3MDkzMDg5NjU1NDcgNjc4Ljg1MDYyOTkyMTI1OTgzNzggVGQKKEpv428gZGEgU2lsdmEpIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKMjM5LjgxNDQ2MDMwODM5NTEyNjcgNjkyLjM1MDYyOTkyMTI1OTgzNzggMTY0LjIwNjg3NjA3MDgxNjY5MDYgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjI0NC44MTQ0NjAzMDgzOTUwOTgzIDY3OC44NTA2Mjk5MjEyNTk4Mzc4IFRkCihT428gUGF1bCkgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwo0MDQuMDIxMzM2Mzc5MjExNzg4OSA2OTIuMzUwNjI5OTIxMjU5ODM3OCA5My41ODAyNjI3MDcwMjQ1NTkyIC0yMS40OTk5OTk5OTk5OTk5OTY0IHJlCkIKQlQKL0YxIDEwIFRmCjExLjUgVEwKMC4zMTQgZwo0MDkuMDIxMzM2Mzc5MjExNzg4OSA2NzguODUwNjI5OTIxMjU5ODM3OCBUZAooKSBUagpFVAoxLiBnCjAuNzggRwowLjI4MzQ2NDU2NjkyOTEzMzkgdwoxLiBnCjQ5Ny42MDE1OTkwODYyMzYzNzY1IDY5Mi4zNTA2Mjk5MjEyNTk4Mzc4IDU3LjY3ODQwMDkxMzc2MzU0NjUgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjUwMi42MDE1OTkwODYyMzYzMTk2IDY3OC44NTA2Mjk5MjEyNTk4Mzc4IFRkCigpIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKNDAuIDY3MC44NTA2Mjk5MjEyNTk4Mzc4IDk3LjQwNTg3MDkzMDg5NjU4MzEgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjQ1LiA2NTcuMzUwNjI5OTIxMjU5ODM3OCBUZAooMjAyNS0wMykgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwoxMzcuNDA1ODcwOTMwODk2NTgzMSA2NzAuODUwNjI5OTIxMjU5ODM3OCAxMDIuNDA4NTg5Mzc3NDk4NTU3OCAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpCCkJUCi9GMSAxMCBUZgoxMS41IFRMCjAuMzE0IGcKMTQyLjQwNTg3MDkzMDg5NjU1NDcgNjU3LjM1MDYyOTkyMTI1OTgzNzggVGQKKEdhYnJpZWwpIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKMjM5LjgxNDQ2MDMwODM5NTEyNjcgNjcwLjg1MDYyOTkyMTI1OTgzNzggMTY0LjIwNjg3NjA3MDgxNjY5MDYgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjI0NC44MTQ0NjAzMDgzOTUwOTgzIDY1Ny4zNTA2Mjk5MjEyNTk4Mzc4IFRkCihDYWNob2VpcmFzIGRlIE1hY2FjdSkgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwo0MDQuMDIxMzM2Mzc5MjExNzg4OSA2NzAuODUwNjI5OTIxMjU5ODM3OCA5My41ODAyNjI3MDcwMjQ1NTkyIC0yMS40OTk5OTk5OTk5OTk5OTY0IHJlCkIKQlQKL0YxIDEwIFRmCjExLjUgVEwKMC4zMTQgZwo0MDkuMDIxMzM2Mzc5MjExNzg4OSA2NTcuMzUwNjI5OTIxMjU5ODM3OCBUZAooKSBUagpFVAoxLiBnCjAuNzggRwowLjI4MzQ2NDU2NjkyOTEzMzkgdwoxLiBnCjQ5Ny42MDE1OTkwODYyMzYzNzY1IDY3MC44NTA2Mjk5MjEyNTk4Mzc4IDU3LjY3ODQwMDkxMzc2MzU0NjUgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjUwMi42MDE1OTkwODYyMzYzMTk2IDY1Ny4zNTA2Mjk5MjEyNTk4Mzc4IFRkCigpIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKNDAuIDY0OS4zNTA2Mjk5MjEyNTk4Mzc4IDk3LjQwNTg3MDkzMDg5NjU4MzEgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjQ1LiA2MzUuODUwNjI5OTIxMjU5ODM3OCBUZAooMjAyNS0wNCkgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwoxMzcuNDA1ODcwOTMwODk2NTgzMSA2NDkuMzUwNjI5OTIxMjU5ODM3OCAxMDIuNDA4NTg5Mzc3NDk4NTU3OCAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpCCkJUCi9GMSAxMCBUZgoxMS41IFRMCjAuMzE0IGcKMTQyLjQwNTg3MDkzMDg5NjU1NDcgNjM1Ljg1MDYyOTkyMTI1OTgzNzggVGQKKEdhYnJpZWwpIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKMjM5LjgxNDQ2MDMwODM5NTEyNjcgNjQ5LjM1MDYyOTkyMTI1OTgzNzggMTY0LjIwNjg3NjA3MDgxNjY5MDYgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjI0NC44MTQ0NjAzMDgzOTUwOTgzIDYzNS44NTA2Mjk5MjEyNTk4Mzc4IFRkCihjYWNob2VpcmFzKSBUagpFVAoxLiBnCjAuNzggRwowLjI4MzQ2NDU2NjkyOTEzMzkgdwoxLiBnCjQwNC4wMjEzMzYzNzkyMTE3ODg5IDY0OS4zNTA2Mjk5MjEyNTk4Mzc4IDkzLjU4MDI2MjcwNzAyNDU1OTIgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjQwOS4wMjEzMzYzNzkyMTE3ODg5IDYzNS44NTA2Mjk5MjEyNTk4Mzc4IFRkCigpIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKNDk3LjYwMTU5OTA4NjIzNjM3NjUgNjQ5LjM1MDYyOTkyMTI1OTgzNzggNTcuNjc4NDAwOTEzNzYzNTQ2NSAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpCCkJUCi9GMSAxMCBUZgoxMS41IFRMCjAuMzE0IGcKNTAyLjYwMTU5OTA4NjIzNjMxOTYgNjM1Ljg1MDYyOTkyMTI1OTgzNzggVGQKKCkgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwo0MC4gNjI3Ljg1MDYyOTkyMTI1OTgzNzggOTcuNDA1ODcwOTMwODk2NTgzMSAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpCCkJUCi9GMSAxMCBUZgoxMS41IFRMCjAuMzE0IGcKNDUuIDYxNC4zNTA2Mjk5MjEyNTk4Mzc4IFRkCihPUy0wMDAxMjc4KSBUagpFVAoxLiBnCjAuNzggRwowLjI4MzQ2NDU2NjkyOTEzMzkgdwoxLiBnCjEzNy40MDU4NzA5MzA4OTY1ODMxIDYyNy44NTA2Mjk5MjEyNTk4Mzc4IDEwMi40MDg1ODkzNzc0OTg1NTc4IC0yMS40OTk5OTk5OTk5OTk5OTY0IHJlCkIKQlQKL0YxIDEwIFRmCjExLjUgVEwKMC4zMTQgZwoxNDIuNDA1ODcwOTMwODk2NTU0NyA2MTQuMzUwNjI5OTIxMjU5ODM3OCBUZAooSm/jbyBkYSBTaWx2YSkgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwoyMzkuODE0NDYwMzA4Mzk1MTI2NyA2MjcuODUwNjI5OTIxMjU5ODM3OCAxNjQuMjA2ODc2MDcwODE2NjkwNiAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpCCkJUCi9GMSAxMCBUZgoxMS41IFRMCjAuMzE0IGcKMjQ0LjgxNDQ2MDMwODM5NTA5ODMgNjE0LjM1MDYyOTkyMTI1OTgzNzggVGQKKFPjbyBQYXVsbykgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwo0MDQuMDIxMzM2Mzc5MjExNzg4OSA2MjcuODUwNjI5OTIxMjU5ODM3OCA5My41ODAyNjI3MDcwMjQ1NTkyIC0yMS40OTk5OTk5OTk5OTk5OTY0IHJlCkIKQlQKL0YxIDEwIFRmCjExLjUgVEwKMC4zMTQgZwo0MDkuMDIxMzM2Mzc5MjExNzg4OSA2MTQuMzUwNjI5OTIxMjU5ODM3OCBUZAooKSBUagpFVAoxLiBnCjAuNzggRwowLjI4MzQ2NDU2NjkyOTEzMzkgdwoxLiBnCjQ5Ny42MDE1OTkwODYyMzYzNzY1IDYyNy44NTA2Mjk5MjEyNTk4Mzc4IDU3LjY3ODQwMDkxMzc2MzU0NjUgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjUwMi42MDE1OTkwODYyMzYzMTk2IDYxNC4zNTA2Mjk5MjEyNTk4Mzc4IFRkCigpIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKNDAuIDYwNi4zNTA2Mjk5MjEyNTk3MjQxIDk3LjQwNTg3MDkzMDg5NjU4MzEgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjQ1LiA1OTIuODUwNjI5OTIxMjU5ODM3OCBUZAooT1MtMDAwMTI4MSkgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwoxMzcuNDA1ODcwOTMwODk2NTgzMSA2MDYuMzUwNjI5OTIxMjU5NzI0MSAxMDIuNDA4NTg5Mzc3NDk4NTU3OCAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpCCkJUCi9GMSAxMCBUZgoxMS41IFRMCjAuMzE0IGcKMTQyLjQwNTg3MDkzMDg5NjU1NDcgNTkyLjg1MDYyOTkyMTI1OTgzNzggVGQKKEpv428gZGEgU2lsdmEpIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKMjM5LjgxNDQ2MDMwODM5NTEyNjcgNjA2LjM1MDYyOTkyMTI1OTcyNDEgMTY0LjIwNjg3NjA3MDgxNjY5MDYgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjI0NC44MTQ0NjAzMDgzOTUwOTgzIDU5Mi44NTA2Mjk5MjEyNTk4Mzc4IFRkCihT428gUGF1bG8zKSBUagpFVAoxLiBnCjAuNzggRwowLjI4MzQ2NDU2NjkyOTEzMzkgdwoxLiBnCjQwNC4wMjEzMzYzNzkyMTE3ODg5IDYwNi4zNTA2Mjk5MjEyNTk3MjQxIDkzLjU4MDI2MjcwNzAyNDU1OTIgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjQwOS4wMjEzMzYzNzkyMTE3ODg5IDU5Mi44NTA2Mjk5MjEyNTk4Mzc4IFRkCigpIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKNDk3LjYwMTU5OTA4NjIzNjM3NjUgNjA2LjM1MDYyOTkyMTI1OTcyNDEgNTcuNjc4NDAwOTEzNzYzNTQ2NSAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpCCkJUCi9GMSAxMCBUZgoxMS41IFRMCjAuMzE0IGcKNTAyLjYwMTU5OTA4NjIzNjMxOTYgNTkyLjg1MDYyOTkyMTI1OTgzNzggVGQKKCkgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwo0MC4gNTg0Ljg1MDYyOTkyMTI1OTgzNzggOTcuNDA1ODcwOTMwODk2NTgzMSAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpCCkJUCi9GMSAxMCBUZgoxMS41IFRMCjAuMzE0IGcKNDUuIDU3MS4zNTA2Mjk5MjEyNTk3MjQxIFRkCigyMDI1LTA1KSBUagpFVAoxLiBnCjAuNzggRwowLjI4MzQ2NDU2NjkyOTEzMzkgdwoxLiBnCjEzNy40MDU4NzA5MzA4OTY1ODMxIDU4NC44NTA2Mjk5MjEyNTk4Mzc4IDEwMi40MDg1ODkzNzc0OTg1NTc4IC0yMS40OTk5OTk5OTk5OTk5OTY0IHJlCkIKQlQKL0YxIDEwIFRmCjExLjUgVEwKMC4zMTQgZwoxNDIuNDA1ODcwOTMwODk2NTU0NyA1NzEuMzUwNjI5OTIxMjU5NzI0MSBUZAooR2FicmllbCkgVGoKRVQKMS4gZwowLjc4IEcKMC4yODM0NjQ1NjY5MjkxMzM5IHcKMS4gZwoyMzkuODE0NDYwMzA4Mzk1MTI2NyA1ODQuODUwNjI5OTIxMjU5ODM3OCAxNjQuMjA2ODc2MDcwODE2NjkwNiAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpCCkJUCi9GMSAxMCBUZgoxMS41IFRMCjAuMzE0IGcKMjQ0LjgxNDQ2MDMwODM5NTA5ODMgNTcxLjM1MDYyOTkyMTI1OTcyNDEgVGQKKENhY2hvZWlyYXMgZGUgTWFjYWN1KSBUagpFVAoxLiBnCjAuNzggRwowLjI4MzQ2NDU2NjkyOTEzMzkgdwoxLiBnCjQwNC4wMjEzMzYzNzkyMTE3ODg5IDU4NC44NTA2Mjk5MjEyNTk4Mzc4IDkzLjU4MDI2MjcwNzAyNDU1OTIgLTIxLjQ5OTk5OTk5OTk5OTk5NjQgcmUKQgpCVAovRjEgMTAgVGYKMTEuNSBUTAowLjMxNCBnCjQwOS4wMjEzMzYzNzkyMTE3ODg5IDU3MS4zNTA2Mjk5MjEyNTk3MjQxIFRkCigpIFRqCkVUCjEuIGcKMC43OCBHCjAuMjgzNDY0NTY2OTI5MTMzOSB3CjEuIGcKNDk3LjYwMTU5OTA4NjIzNjM3NjUgNTg0Ljg1MDYyOTkyMTI1OTgzNzggNTcuNjc4NDAwOTEzNzYzNTQ2NSAtMjEuNDk5OTk5OTk5OTk5OTk2NCByZQpCCkJUCi9GMSAxMCBUZgoxMS41IFRMCjAuMzE0IGcKNTAyLjYwMTU5OTA4NjIzNjMxOTYgNTcxLjM1MDYyOTkyMTI1OTcyNDEgVGQKKCkgVGoKRVQKMC4gRwowLjU2NzAwMDAwMDAwMDAwMDEgdwowLjc4IEcKMC4gdwowLiBHCjAuNTY3MDAwMDAwMDAwMDAwMSB3CjAuIEcKMC41NjcwMDAwMDAwMDAwMDAxIHcKZW5kc3RyZWFtCmVuZG9iagoxIDAgb2JqCjw8L1R5cGUgL1BhZ2VzCi9LaWRzIFszIDAgUiBdCi9Db3VudCAxCj4+CmVuZG9iago1IDAgb2JqCjw8Ci9UeXBlIC9Gb250Ci9CYXNlRm9udCAvSGVsdmV0aWNhCi9TdWJ0eXBlIC9UeXBlMQovRW5jb2RpbmcgL1dpbkFuc2lFbmNvZGluZwovRmlyc3RDaGFyIDMyCi9MYXN0Q2hhciAyNTUKPj4KZW5kb2JqCjYgMCBvYmoKPDwKL1R5cGUgL0ZvbnQKL0Jhc2VGb250IC9IZWx2ZXRpY2EtQm9sZAovU3VidHlwZSAvVHlwZTEKL0VuY29kaW5nIC9XaW5BbnNpRW5jb2RpbmcKL0ZpcnN0Q2hhciAzMgovTGFzdENoYXIgMjU1Cj4+CmVuZG9iago3IDAgb2JqCjw8Ci9UeXBlIC9Gb250Ci9CYXNlRm9udCAvSGVsdmV0aWNhLU9ibGlxdWUKL1N1YnR5cGUgL1R5cGUxCi9FbmNvZGluZyAvV2luQW5zaUVuY29kaW5nCi9GaXJzdENoYXIgMzIKL0xhc3RDaGFyIDI1NQo+PgplbmRvYmoKOCAwIG9iago8PAovVHlwZSAvRm9udAovQmFzZUZvbnQgL0hlbHZldGljYS1Cb2xkT2JsaXF1ZQovU3VidHlwZSAvVHlwZTEKL0VuY29kaW5nIC9XaW5BbnNpRW5jb2RpbmcKL0ZpcnN0Q2hhciAzMgovTGFzdENoYXIgMjU1Cj4+CmVuZG9iago5IDAgb2JqCjw8Ci9UeXBlIC9Gb250Ci9CYXNlRm9udCAvQ291cmllcgovU3VidHlwZSAvVHlwZTEKL0VuY29kaW5nIC9XaW5BbnNpRW5jb2RpbmcKL0ZpcnN0Q2hhciAzMgovTGFzdENoYXIgMjU1Cj4+CmVuZG9iagoxMCAwIG9iago8PAovVHlwZSAvRm9udAovQmFzZUZvbnQgL0NvdXJpZXItQm9sZAovU3VidHlwZSAvVHlwZTEKL0VuY29kaW5nIC9XaW5BbnNpRW5jb2RpbmcKL0ZpcnN0Q2hhciAzMgovTGFzdENoYXIgMjU1Cj4+CmVuZG9iagoxMSAwIG9iago8PAovVHlwZSAvRm9udAovQmFzZUZvbnQgL0NvdXJpZXItT2JsaXF1ZQovU3VidHlwZSAvVHlwZTEKL0VuY29kaW5nIC9XaW5BbnNpRW5jb2RpbmcKL0ZpcnN0Q2hhciAzMgovTGFzdENoYXIgMjU1Cj4+CmVuZG9iagoxMiAwIG9iago8PAovVHlwZSAvRm9udAovQmFzZUZvbnQgL0NvdXJpZXItQm9sZE9ibGlxdWUKL1N1YnR5cGUgL1R5cGUxCi9FbmNvZGluZyAvV2luQW5zaUVuY29kaW5nCi9GaXJzdENoYXIgMzIKL0xhc3RDaGFyIDI1NQo+PgplbmRvYmoKMTMgMCBvYmoKPDwKL1R5cGUgL0ZvbnQKL0Jhc2VGb250IC9UaW1lcy1Sb21hbgovU3VidHlwZSAvVHlwZTEKL0VuY29kaW5nIC9XaW5BbnNpRW5jb2RpbmcKL0ZpcnN0Q2hhciAzMgovTGFzdENoYXIgMjU1Cj4+CmVuZG9iagoxNCAwIG9iago8PAovVHlwZSAvRm9udAovQmFzZUZvbnQgL1RpbWVzLUJvbGQKL1N1YnR5cGUgL1R5cGUxCi9FbmNvZGluZyAvV2luQW5zaUVuY29kaW5nCi9GaXJzdENoYXIgMzIKL0xhc3RDaGFyIDI1NQo+PgplbmRvYmoKMTUgMCBvYmoKPDwKL1R5cGUgL0ZvbnQKL0Jhc2VGb250IC9UaW1lcy1JdGFsaWMKL1N1YnR5cGUgL1R5cGUxCi9FbmNvZGluZyAvV2luQW5zaUVuY29kaW5nCi9GaXJzdENoYXIgMzIKL0xhc3RDaGFyIDI1NQo+PgplbmRvYmoKMTYgMCBvYmoKPDwKL1R5cGUgL0ZvbnQKL0Jhc2VGb250IC9UaW1lcy1Cb2xkSXRhbGljCi9TdWJ0eXBlIC9UeXBlMQovRW5jb2RpbmcgL1dpbkFuc2lFbmNvZGluZwovRmlyc3RDaGFyIDMyCi9MYXN0Q2hhciAyNTUKPj4KZW5kb2JqCjE3IDAgb2JqCjw8Ci9UeXBlIC9Gb250Ci9CYXNlRm9udCAvWmFwZkRpbmdiYXRzCi9TdWJ0eXBlIC9UeXBlMQovRmlyc3RDaGFyIDMyCi9MYXN0Q2hhciAyNTUKPj4KZW5kb2JqCjE4IDAgb2JqCjw8Ci9UeXBlIC9Gb250Ci9CYXNlRm9udCAvU3ltYm9sCi9TdWJ0eXBlIC9UeXBlMQovRmlyc3RDaGFyIDMyCi9MYXN0Q2hhciAyNTUKPj4KZW5kb2JqCjIgMCBvYmoKPDwKL1Byb2NTZXQgWy9QREYgL1RleHQgL0ltYWdlQiAvSW1hZ2VDIC9JbWFnZUldCi9Gb250IDw8Ci9GMSA1IDAgUgovRjIgNiAwIFIKL0YzIDcgMCBSCi9GNCA4IDAgUgovRjUgOSAwIFIKL0Y2IDEwIDAgUgovRjcgMTEgMCBSCi9GOCAxMiAwIFIKL0Y5IDEzIDAgUgovRjEwIDE0IDAgUgovRjExIDE1IDAgUgovRjEyIDE2IDAgUgovRjEzIDE3IDAgUgovRjE0IDE4IDAgUgo+PgovWE9iamVjdCA8PAo+Pgo+PgplbmRvYmoKMTkgMCBvYmoKPDwKL1Byb2R1Y2VyIChqc1BERiAzLjAuMykKL0NyZWF0aW9uRGF0ZSAoRDoyMDI1MTExODEzNDkxOC0wMycwMCcpCj4+CmVuZG9iagoyMCAwIG9iago8PAovVHlwZSAvQ2F0YWxvZwovUGFnZXMgMSAwIFIKL09wZW5BY3Rpb24gWzMgMCBSIC9GaXRIIG51bGxdCi9QYWdlTGF5b3V0IC9PbmVDb2x1bW4KPj4KZW5kb2JqCnhyZWYKMCAyMQowMDAwMDAwMDAwIDY1NTM1IGYgCjAwMDAwMDk5MzUgMDAwMDAgbiAKMDAwMDAxMTc1MiAwMDAwMCBuIAowMDAwMDAwMDE1IDAwMDAwIG4gCjAwMDAwMDAxNTIgMDAwMDAgbiAKMDAwMDAwOTk5MiAwMDAwMCBuIAowMDAwMDEwMTE3IDAwMDAwIG4gCjAwMDAwMTAyNDcgMDAwMDAgbiAKMDAwMDAxMDM4MCAwMDAwMCBuIAowMDAwMDEwNTE3IDAwMDAwIG4gCjAwMDAwMTA2NDAgMDAwMDAgbiAKMDAwMDAxMDc2OSAwMDAwMCBuIAowMDAwMDEwOTAxIDAwMDAwIG4gCjAwMDAwMTEwMzcgMDAwMDAgbiAKMDAwMDAxMTE2NSAwMDAwMCBuIAowMDAwMDExMjkyIDAwMDAwIG4gCjAwMDAwMTE0MjEgMDAwMDAgbiAKMDAwMDAxMTU1NCAwMDAwMCBuIAowMDAwMDExNjU2IDAwMDAwIG4gCjAwMDAwMTIwMDAgMDAwMDAgbiAKMDAwMDAxMjA4NiAwMDAwMCBuIAp0cmFpbGVyCjw8Ci9TaXplIDIxCi9Sb290IDIwIDAgUgovSW5mbyAxOSAwIFIKL0lEIFsgPDUxNkMyMkE4MDY3QTdBQjFDNjdGOEExRDM2RjJCRkYwPiA8NTE2QzIyQTgwNjdBN0FCMUM2N0Y4QTFEMzZGMkJGRjA+IF0KPj4Kc3RhcnR4cmVmCjEyMTkwCiUlRU9G"
            }
        }
    };

    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.DefaultRequestHeaders.Add("api-key", builder.Configuration.GetValue<string>("email-key"));
    

    var result = await client.PostAsync(brevoEndpoint, new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

    return Results.Ok(await result.Content.ReadAsStringAsync());
});





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

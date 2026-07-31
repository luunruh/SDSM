using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    public record LoginRequest(string Username, string Password);

    [HttpGet]
    [Route("status")]
    [AllowAnonymous]
    public IResult Status(AuthService auth)
    {
        bool authenticated = User.Identity?.IsAuthenticated ?? false;
        return Results.Ok(new
        {
            authenticated,
            setupRequired = auth.SetupRequired,
            username = authenticated ? User.Identity!.Name : null,
        });
    }

    // First-run only: creates the admin account and signs in.
    [HttpPost]
    [Route("setup")]
    [AllowAnonymous]
    public async Task<IResult> Setup(AuthService auth, LoginRequest request)
    {
        if (!auth.SetupRequired)
        {
            return Results.Conflict();
        }
        if (string.IsNullOrWhiteSpace(request.Username) || request.Password.Length < 8)
        {
            return Results.BadRequest();
        }
        auth.SetPassword(request.Username.Trim(), request.Password);
        await SignIn(request.Username.Trim());
        return Results.NoContent();
    }

    [HttpPost]
    [Route("login")]
    [AllowAnonymous]
    public async Task<IResult> Login(AuthService auth, LoginRequest request)
    {
        if (!auth.Verify(request.Username, request.Password))
        {
            return Results.Unauthorized();
        }
        await SignIn(request.Username);
        return Results.NoContent();
    }

    [HttpPost]
    [Route("logout")]
    public async Task<IResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.NoContent();
    }

    private Task SignIn(string username)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, username)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }
}

using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Safar.Application.Common;
using Safar.Domain.Entities;
using Safar.Domain.Enums;

namespace Safar.Application.Auth;

public record RegisterRequest(string FullName, string Email, string Password, string? Phone);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record UserDto(Guid Id, string FullName, string Email, string? Phone, UserRole Role);
public record AuthResponse(string AccessToken, DateTime ExpiresAt, string RefreshToken, UserDto User);

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().Length(3, 100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a number.");
        RuleFor(x => x.Phone).MaximumLength(20);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);
    Task LogoutAsync(RefreshRequest request, CancellationToken ct = default);
    Task<UserDto> GetCurrentAsync(CancellationToken ct = default);
}

public class AuthService(
    IAppDbContext db,
    IPasswordService passwords,
    ITokenService tokens,
    ICurrentUser currentUser,
    TimeProvider clock,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator) : IAuthService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        await registerValidator.ValidateAndThrowAsync(request, ct);

        var email = Normalize(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("An account with this email already exists.");

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = request.Phone?.Trim(),
            Role = UserRole.Customer,
            CreatedAt = Now
        };
        user.PasswordHash = passwords.Hash(user, request.Password);
        db.Users.Add(user);

        return await IssueTokensAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        await loginValidator.ValidateAndThrowAsync(request, ct);

        var email = Normalize(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !passwords.Verify(user, request.Password))
            throw new UnauthorizedException("Invalid email or password.");

        return await IssueTokensAsync(user, ct);
    }

    /// <summary>Exchanges a valid refresh token for a new access token and rotates the refresh token.</summary>
    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var hash = tokens.Hash(request.RefreshToken);
        var stored = await db.RefreshTokens.Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || !stored.IsActive(Now))
            throw new UnauthorizedException("Session expired. Please sign in again.");

        stored.RevokedAt = Now; // rotation: each refresh token can be used once
        return await IssueTokensAsync(stored.User, ct);
    }

    public async Task LogoutAsync(RefreshRequest request, CancellationToken ct = default)
    {
        var hash = tokens.Hash(request.RefreshToken);
        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = Now;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<UserDto> GetCurrentAsync(CancellationToken ct = default)
    {
        var id = currentUser.UserId ?? throw new UnauthorizedException("Not signed in.");
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct)
                   ?? throw new NotFoundException("User not found.");
        return ToDto(user);
    }

    private async Task<AuthResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var (access, expires) = tokens.CreateAccessToken(user);
        var (raw, hash) = tokens.CreateRefreshToken();

        db.RefreshTokens.Add(new RefreshToken
        {
            User = user,
            UserId = user.Id,
            TokenHash = hash,
            CreatedAt = Now,
            ExpiresAt = Now.Add(tokens.RefreshTokenLifetime)
        });
        await db.SaveChangesAsync(ct);

        return new AuthResponse(access, expires, raw, ToDto(user));
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    internal static UserDto ToDto(User u) => new(u.Id, u.FullName, u.Email, u.Phone, u.Role);
}

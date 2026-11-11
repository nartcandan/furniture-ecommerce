using FluentValidation;
using FluentValidation.Results;
using FurnitureEcommerce.Application.Auth;
using FurnitureEcommerce.Application.Common.Exceptions;
using FurnitureEcommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FurnitureEcommerce.Infrastructure.Identity;

public class IdentityService(
    UserManager<ApplicationUser> userManager,
    AppDbContext dbContext) : IIdentityService
{
    private const string EmailTakenMessage = "This email address is already registered.";

    public async Task<AuthUser> CreateUserAsync(NewUser newUser)
    {
        // UserName has a unique index and Email does not, so it stops concurrent duplicates.
        var user = new ApplicationUser
        {
            UserName = newUser.Email,
            Email = newUser.Email,
            FirstName = newUser.FirstName,
            LastName = newUser.LastName
        };

        // One transaction, so a failed role assignment does not leave a user without a role.
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        IdentityResult result;

        try
        {
            result = await userManager.CreateAsync(user, newUser.Password);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException(EmailTakenMessage);
        }

        if (!result.Succeeded)
        {
            ThrowForErrors(result.Errors);
        }

        var roleResult = await userManager.AddToRoleAsync(user, Roles.Customer);

        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException($"The '{Roles.Customer}' role could not be assigned.");
        }

        await transaction.CommitAsync();

        return await ToAuthUserAsync(user);
    }

    public async Task<AuthUser?> ValidateCredentialsAsync(string email, string password)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            return null;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return null;
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return await ToAuthUserAsync(user);
    }

    public async Task<AuthUser?> GetUserAsync(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());

        return user is null ? null : await ToAuthUserAsync(user);
    }

    private static void ThrowForErrors(IEnumerable<IdentityError> errors)
    {
        var errorList = errors.ToList();

        if (errorList.Any(error => error.Code is
            nameof(IdentityErrorDescriber.DuplicateEmail) or nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            throw new ConflictException(EmailTakenMessage);
        }

        throw new ValidationException(errorList.Select(error => new ValidationFailure(
            error.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password" : "Email",
            error.Description)));
    }

    private async Task<AuthUser> ToAuthUserAsync(ApplicationUser user)
    {
        var roles = await userManager.GetRolesAsync(user);

        return new AuthUser(user.Id, user.Email!, user.FirstName, user.LastName, roles.ToList());
    }
}

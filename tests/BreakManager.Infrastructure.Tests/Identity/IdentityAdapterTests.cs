using BreakManager.Application.Identity;
using BreakManager.Domain.Identity;
using BreakManager.Infrastructure.Identity;
using BreakManager.Infrastructure.Security;
using BreakManager.Infrastructure.Tests.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace BreakManager.Infrastructure.Tests.Identity;

public sealed class SecretAdapterTests
{
    private readonly TokenService _tokens = new();

    [Fact]
    public void GenerateToken_Is256BitBase64Url_AndUnique()
    {
        var a = _tokens.GenerateToken();
        var b = _tokens.GenerateToken();

        Assert.Equal(43, a.Length);
        Assert.DoesNotContain(a, c => c is '+' or '/' or '=');
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void GenerateEnrollmentCode_HasTenUnambiguousCharacters()
    {
        var code = _tokens.GenerateEnrollmentCode();

        Assert.Equal(10, code.Length);
        Assert.DoesNotContain(code, c => c is '0' or 'O' or '1' or 'I' or 'L');
        Assert.All(code, c => Assert.True(char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c)));
    }

    [Fact]
    public void Hash_IsSha256Hex_AndDeterministic()
    {
        var hash = _tokens.Hash("secreto");

        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, _tokens.Hash("secreto"));
        Assert.NotEqual(hash, _tokens.Hash("otro"));
    }

    [Fact]
    public void PinHasher_UsesASaltedHash_AndVerifies()
    {
        var hasher = new Pbkdf2PinHasher();
        var pin = Pin.Create("482913", EmployeeNumber.Create("20481").Value).Value;

        var first = hasher.Hash(pin);
        var second = hasher.Hash(pin);

        Assert.NotEqual(first, second);
        Assert.DoesNotContain("482913", first, StringComparison.Ordinal);
        Assert.True(hasher.Verify(first, "482913"));
        Assert.False(hasher.Verify(first, "482914"));
        Assert.False(hasher.Verify(first, string.Empty));
    }

    [Theory]
    [InlineData("1qaz2wsx3edc")]
    [InlineData("QWERTY123456")]
    public void CommonPasswordValidator_KnowsCommonPasswords_IgnoringCase(string password)
    {
        Assert.True(CommonPasswordValidator.IsCommon(password));
    }

    [Fact]
    public void CommonPasswordValidator_AcceptsAnUncommonPassphrase()
    {
        Assert.False(CommonPasswordValidator.IsCommon("mate-cocido-con-tostadas-7"));
    }
}

public sealed class UserAccountServiceTests(PostgreSqlFixture server) : IClassFixture<PostgreSqlFixture>
{
    private const string GoodPassword = "mate-cocido-con-tostadas-7";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_StoresRoleGroupAndMustChangePassword()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        await using var scope = db.NewScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAccountService>();

        var created = await users.CreateAsync(new NewUserAccount("supervisor.c", WebRole.Supervisor, Group.C, GoodPassword), Ct);

        Assert.True(created.IsSuccess);
        var summary = await users.GetAsync(created.Value, Ct);
        Assert.Equal(new UserAccountSummary(created.Value, "supervisor.c", WebRole.Supervisor, Group.C, true, false), summary);
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(created.Value.ToString());
        Assert.True(user!.MustChangePassword);
    }

    [Theory]
    [InlineData("corta-1", "Identity.PasswordTooShort")]
    [InlineData("1qaz2wsx3edc", "Identity.CommonPassword")]
    public async Task Create_RejectsShortOrCommonPasswords_InSpanish(string password, string expectedCode)
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        await using var scope = db.NewScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAccountService>();

        var created = await users.CreateAsync(new NewUserAccount("calidad", WebRole.Quality, null, password), Ct);

        Assert.True(created.IsFailure);
        Assert.Equal(expectedCode, created.Error.Code);
        Assert.Contains("contraseña", created.Error.Message, StringComparison.Ordinal);
        Assert.Empty(await users.ListAsync(Ct));
    }

    [Fact]
    public async Task Create_UsesTheConfiguredMinimumLength()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        await using var scope = db.NewScope();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<IdentityOptions>>();

        Assert.Equal(new IdentityParameters().WebPasswordMinLength, options.Value.Password.RequiredLength);
        Assert.Equal(new IdentityParameters().WebLockoutAttempts, options.Value.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(new IdentityParameters().WebLockoutDuration, options.Value.Lockout.DefaultLockoutTimeSpan);
    }

    [Fact]
    public async Task SetActive_And_UpdateRole_ChangeTheSecurityStamp()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        await using var scope = db.NewScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAccountService>();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var id = (await users.CreateAsync(new NewUserAccount("coord", WebRole.Coordinator, null, GoodPassword), Ct)).Value;
        var stamp0 = (await manager.FindByIdAsync(id.ToString()))!.SecurityStamp;

        await users.SetActiveAsync(id, false, Ct);
        var stamp1 = (await manager.FindByIdAsync(id.ToString()))!.SecurityStamp;
        await users.UpdateRoleAsync(id, WebRole.Supervisor, Group.A, Ct);
        var stamp2 = (await manager.FindByIdAsync(id.ToString()))!.SecurityStamp;

        Assert.NotEqual(stamp0, stamp1);
        Assert.NotEqual(stamp1, stamp2);
        var summary = await users.GetAsync(id, Ct);
        Assert.False(summary!.Active);
        Assert.Equal(WebRole.Supervisor, summary.Role);
        Assert.Equal(Group.A, summary.Group);
    }

    [Fact]
    public async Task ResetPassword_WithACommonPassword_KeepsTheCurrentOne()
    {
        await using var db = await TestDatabase.CreateAsync(server, Ct);
        await using var scope = db.NewScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserAccountService>();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var id = (await users.CreateAsync(new NewUserAccount("calidad", WebRole.Quality, null, GoodPassword), Ct)).Value;

        var rejected = await users.ResetPasswordAsync(id, "qwerty123456", Ct);
        var accepted = await users.ResetPasswordAsync(id, "otra-frase-bastante-larga-9", Ct);

        Assert.True(rejected.IsFailure);
        Assert.True(accepted.IsSuccess);
        var user = await manager.FindByIdAsync(id.ToString());
        Assert.True(await manager.CheckPasswordAsync(user!, "otra-frase-bastante-larga-9"));
        Assert.True(user!.MustChangePassword);
    }
}

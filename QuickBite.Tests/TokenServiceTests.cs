using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Moq;
using QuickBite.API.Models;
using QuickBite.API.Services;
using Xunit;

namespace QuickBite.Tests;

public class TokenServiceTests
{
    private readonly TokenService _sut;
    private const string TestKey = "TestSecretKey-AtLeast32Characters-Long!";

    public TokenServiceTests()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c["Jwt:Key"]).Returns(TestKey);
        config.Setup(c => c["Jwt:Issuer"]).Returns("TestIssuer");
        config.Setup(c => c["Jwt:Audience"]).Returns("TestAudience");
        config.Setup(c => c["Jwt:ExpiresInMinutes"]).Returns("2");
        _sut = new TokenService(config.Object);
    }

    private static ApplicationUser MakeUser(UserRole role = UserRole.Customer) => new()
    {
        Id = "blablabla",
        Email = "blabla@lalala.com",
        FullName = "blabla lala",
        Role = role
    };

    [Fact]
    public void GenerateToken_ReturnsNonEmptyString()
    {
        var token = _sut.GenerateToken(MakeUser());
        Assert.NotEmpty(token);
    }

    [Fact]
    public void GenerateToken_ReturnsValidJwtFormat()
    {
        var token = _sut.GenerateToken(MakeUser());
        Assert.Equal(3, token.Split('.').Length);
    }

    [Fact]
    public void GenerateToken_ContainsEmailClaim()
    {
        var token = _sut.GenerateToken(MakeUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "blabla@lalala.com");
    }

    [Fact]
    public void GenerateToken_ContainsSubClaim()
    {
        var token = _sut.GenerateToken(MakeUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "blablabla");
    }

    [Theory]
    [InlineData(UserRole.Customer,   "Customer")]
    [InlineData(UserRole.Restaurant, "Restaurant")]
    [InlineData(UserRole.Courier,    "Courier")]
    public void GenerateToken_ContainsCorrectRoleClaim(UserRole role, string expectedRoleName)
    {
        var token = _sut.GenerateToken(MakeUser(role));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == expectedRoleName);
    }

    [Fact]
    public void GenerateToken_ContainsFullNameClaim()
    {
        var token = _sut.GenerateToken(MakeUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Contains(jwt.Claims, c => c.Type == "fullName" && c.Value == "blabla lala");
    }

    [Fact]
    public void GenerateToken_ExpiresAccordingToConfig()
    {
        var token = _sut.GenerateToken(MakeUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var expectedExpiry = DateTime.UtcNow.AddMinutes(2);
        Assert.True(Math.Abs((jwt.ValidTo - expectedExpiry).TotalSeconds) < 10);
    }

    [Fact]
    public void GenerateToken_HasCorrectIssuer()
    {
        var token = _sut.GenerateToken(MakeUser());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("TestIssuer", jwt.Issuer);
    }

    [Fact]
    public void GenerateToken_TwoCallsProduceDifferentTokens()
    {
        var user = MakeUser();
        var token1 = _sut.GenerateToken(user);
        var token2 = _sut.GenerateToken(user);
        Assert.NotEqual(token1, token2);
    }
}

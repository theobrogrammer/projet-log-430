using ProjetLog430.Application.Services;
using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Model.Securite;
using ProjetLog430.Domain.Model.PortefeuilleReglement;
using ProjetLog430.Domain.Model.Observabilite;
using ProjetLog430.Domain.Ports.Outbound;
using ProjetLog430.Domain.Contracts;
using Moq;

namespace Application.Tests;

public class SignupServiceTests
{
    private readonly Mock<IClientRepository> _mockClientRepository;
    private readonly Mock<IAccountRepository> _mockAccountRepository;
    private readonly Mock<IPortfolioRepository> _mockPortfolioRepository;
    private readonly Mock<IKycPort> _mockKycPort;
    private readonly Mock<IOtpPort> _mockOtpPort;
    private readonly Mock<IAuditPort> _mockAuditPort;
    private readonly SignupService _signupService;

    public SignupServiceTests()
    {
        _mockClientRepository = new Mock<IClientRepository>();
        _mockAccountRepository = new Mock<IAccountRepository>();
        _mockPortfolioRepository = new Mock<IPortfolioRepository>();
        _mockKycPort = new Mock<IKycPort>();
        _mockOtpPort = new Mock<IOtpPort>();
        _mockAuditPort = new Mock<IAuditPort>();

        _signupService = new SignupService(
            _mockClientRepository.Object,
            _mockAccountRepository.Object,
            _mockPortfolioRepository.Object,
            _mockKycPort.Object,
            _mockOtpPort.Object,
            _mockAuditPort.Object);
    }

    [Fact]
    public async Task CreateAccountAsync_ValidData_ReturnsSuccessResult()
    {
        // Arrange
        var email = "test@example.com";
        var phone = "1234567890";
        var fullName = "John Doe";
        var password = "TestPass123!";
        var birthDate = new DateOnly(1990, 1, 1);

        _mockClientRepository.Setup(x => x.AddAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAccountRepository.Setup(x => x.AddAsync(It.IsAny<Compte>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockPortfolioRepository.Setup(x => x.AddAsync(It.IsAny<Portefeuille>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockKycPort.Setup(x => x.SubmitAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockOtpPort.Setup(x => x.SendContactOtpAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CanalOTP>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAuditPort.Setup(x => x.WriteAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _signupService.CreateAccountAsync(email, phone, fullName, password, birthDate);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.ClientId);
        Assert.NotEqual(Guid.Empty, result.AccountId);
        Assert.Equal("Pending", result.Status);

        // Verify all repositories were called
        _mockClientRepository.Verify(x => x.AddAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockAccountRepository.Verify(x => x.AddAsync(It.IsAny<Compte>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockPortfolioRepository.Verify(x => x.AddAsync(It.IsAny<Portefeuille>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAccountAsync_ValidDataWithoutBirthDate_ReturnsSuccessResult()
    {
        // Arrange
        var email = "test@example.com";
        var phone = "1234567890";
        var fullName = "John Doe";
        var password = "TestPass123!";
        DateOnly? birthDate = null;

        _mockClientRepository.Setup(x => x.AddAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAccountRepository.Setup(x => x.AddAsync(It.IsAny<Compte>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockPortfolioRepository.Setup(x => x.AddAsync(It.IsAny<Portefeuille>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockKycPort.Setup(x => x.SubmitAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockOtpPort.Setup(x => x.SendContactOtpAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CanalOTP>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAuditPort.Setup(x => x.WriteAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _signupService.CreateAccountAsync(email, phone, fullName, password, birthDate);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.ClientId);
        Assert.NotEqual(Guid.Empty, result.AccountId);
        Assert.Equal("Pending", result.Status);
    }

    [Fact]
    public async Task CreateAccountAsync_PasswordIsHashedSecurely()
    {
        // Arrange
        var email = "test@example.com";
        var phone = "1234567890";
        var fullName = "John Doe";
        var plainPassword = "TestPass123!";
        var birthDate = new DateOnly(1990, 1, 1);

        Client? capturedClient = null;
        _mockClientRepository.Setup(x => x.AddAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()))
            .Callback<Client, CancellationToken>((client, ct) => capturedClient = client)
            .Returns(Task.CompletedTask);
        _mockAccountRepository.Setup(x => x.AddAsync(It.IsAny<Compte>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockPortfolioRepository.Setup(x => x.AddAsync(It.IsAny<Portefeuille>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _signupService.CreateAccountAsync(email, phone, fullName, plainPassword, birthDate);

        // Assert
        Assert.NotNull(capturedClient);
        Assert.True(capturedClient.VerifyPassword(plainPassword));
        Assert.False(capturedClient.VerifyPassword("WrongPassword"));
        
        // Verify password was hashed (not stored in plain text)
        Assert.NotEqual(plainPassword, capturedClient.PasswordHash);
        Assert.StartsWith("$2", capturedClient.PasswordHash); // BCrypt hash prefix
    }

    [Theory]
    [InlineData("theodor-george.trif.1@ens.etsmtl.ca")]
    [InlineData("user+tag@example.org")]
    [InlineData("test.email@sub.domain.com")]
    public async Task CreateAccountAsync_ComplexEmails_HandlesCorrectly(string email)
    {
        // Arrange
        var phone = "1234567890";
        var fullName = "Test User";
        var password = "TestPass123!";
        var birthDate = new DateOnly(1990, 1, 1);

        _mockClientRepository.Setup(x => x.AddAsync(It.IsAny<Client>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockAccountRepository.Setup(x => x.AddAsync(It.IsAny<Compte>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockPortfolioRepository.Setup(x => x.AddAsync(It.IsAny<Portefeuille>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _signupService.CreateAccountAsync(email, phone, fullName, password, birthDate);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.ClientId);
        Assert.NotEqual(Guid.Empty, result.AccountId);
        Assert.Equal("Pending", result.Status);
    }
}

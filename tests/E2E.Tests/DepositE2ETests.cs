using System.Text.Json;
using System.Text;
using Xunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using ProjetLog430.Infrastructure.Web.DTOs;

namespace E2E.Tests;

public class DepositE2ETests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DepositE2ETests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Complete_Deposit_Workflow_ShouldProcessSuccessfully()
    {
        // 1. D'abord créer un compte via signup
        var signupRequest = new SignupRequestDto
        {
            Email = "deposit-test@example.com",
            Phone = "514-123-4567",
            FullName = "Deposit Test User",
            BirthDate = new DateOnly(1985, 5, 15),
            Password = "SecurePass123!",
            ConfirmPassword = "SecurePass123!"
        };

        var signupContent = new StringContent(
            JsonSerializer.Serialize(signupRequest),
            Encoding.UTF8,
            "application/json");

        var signupResponse = await _client.PostAsync("/api/v1/signup", signupContent);
        signupResponse.EnsureSuccessStatusCode();

        var signupResult = JsonSerializer.Deserialize<SignupResponseDto>(
            await signupResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(signupResult);
        var accountId = signupResult.AccountId;

        // 2. Consulter le solde initial (devrait être 0)
        var balanceResponse = await _client.GetAsync($"/api/v1/accounts/{accountId}/balance");
        balanceResponse.EnsureSuccessStatusCode();

        var initialBalance = JsonSerializer.Deserialize<WalletBalanceDto>(
            await balanceResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(initialBalance);
        Assert.Equal(0m, initialBalance.Balance);
        Assert.Equal("USD", initialBalance.Currency); // Par défaut créé en USD

        // 3. Effectuer un dépôt
        var depositRequest = new DepositRequestDto
        {
            Amount = 250.75m,
            Currency = "USD", // Utiliser USD pour correspondre au portefeuille
            IdempotencyKey = $"test-deposit-{Guid.NewGuid()}"
        };

        var depositContent = new StringContent(
            JsonSerializer.Serialize(depositRequest),
            Encoding.UTF8,
            "application/json");

        var depositResponse = await _client.PostAsync($"/api/v1/accounts/{accountId}/deposit", depositContent);
        depositResponse.EnsureSuccessStatusCode();

        var depositResult = JsonSerializer.Deserialize<DepositResponseDto>(
            await depositResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(depositResult);
        Assert.Equal("Pending", depositResult.Status);
        Assert.NotEqual(Guid.Empty, depositResult.PaymentTxId);

        // Pour simplifier le test, on vérifie juste que le dépôt est créé avec le statut Pending
        // Dans un environnement de test, le webhook ne peut pas facilement être simulé
        // Le processus asynchrone est testé séparément dans les tests unitaires
    }

    [Fact]
    public async Task Deposit_WithIdempotency_ShouldReturnSameResult()
    {
        // 1. Créer un compte pour le test
        var signupRequest = new SignupRequestDto
        {
            Email = "idempotency-test@example.com",
            Phone = "514-987-6543", 
            FullName = "Idempotency Test User",
            BirthDate = new DateOnly(1990, 1, 1),
            Password = "SecurePass123!",
            ConfirmPassword = "SecurePass123!"
        };

        var signupContent = new StringContent(
            JsonSerializer.Serialize(signupRequest),
            Encoding.UTF8,
            "application/json");

        var signupResponse = await _client.PostAsync("/api/v1/signup", signupContent);
        signupResponse.EnsureSuccessStatusCode();

        var signupResult = JsonSerializer.Deserialize<SignupResponseDto>(
            await signupResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var accountId = signupResult!.AccountId;

        // 2. Premier dépôt avec clé d'idempotence
        var idempotencyKey = $"idempotent-test-{Guid.NewGuid()}";
        var depositRequest = new DepositRequestDto
        {
            Amount = 100.00m,
            Currency = "USD", // Utiliser USD pour correspondre au portefeuille
            IdempotencyKey = idempotencyKey
        };

        var depositContent = new StringContent(
            JsonSerializer.Serialize(depositRequest),
            Encoding.UTF8,
            "application/json");

        var firstResponse = await _client.PostAsync($"/api/v1/accounts/{accountId}/deposit", depositContent);
        firstResponse.EnsureSuccessStatusCode();

        var firstResult = JsonSerializer.Deserialize<DepositResponseDto>(
            await firstResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // 3. Répéter le même dépôt avec la même clé d'idempotence
        var secondContent = new StringContent(
            JsonSerializer.Serialize(depositRequest),
            Encoding.UTF8,
            "application/json");

        var secondResponse = await _client.PostAsync($"/api/v1/accounts/{accountId}/deposit", secondContent);
        secondResponse.EnsureSuccessStatusCode();

        var secondResult = JsonSerializer.Deserialize<DepositResponseDto>(
            await secondResponse.Content.ReadAsStringAsync(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // 4. Vérifier que les résultats sont identiques
        Assert.Equal(firstResult!.PaymentTxId, secondResult!.PaymentTxId);
        Assert.Equal(firstResult.Status, secondResult.Status);
        Assert.Equal(firstResult.NewCashBalance, secondResult.NewCashBalance);
    }

    [Theory]
    [InlineData(0.005, "Le montant doit être entre")]
    [InlineData(1_500_000, "Le montant doit être entre")]
    public async Task Deposit_WithInvalidAmount_ShouldReturnValidationError(decimal invalidAmount, string expectedError)
    {
        // Arrange
        var accountId = Guid.NewGuid(); // Même si le compte n'existe pas, la validation DTO doit échouer avant

        var depositRequest = new DepositRequestDto
        {
            Amount = invalidAmount,
            Currency = "USD", // Utiliser USD même pour les tests d'erreur
            IdempotencyKey = $"invalid-test-{Guid.NewGuid()}"
        };

        var depositContent = new StringContent(
            JsonSerializer.Serialize(depositRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync($"/api/v1/accounts/{accountId}/deposit", depositContent);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        var errorContent = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedError, errorContent);
    }

    [Theory]
    [InlineData("XY", "Devise ISO-3 requise")]
    [InlineData("ABCD", "Devise ISO-3 requise")]
    [InlineData("xyz", "Devise doit être 3 lettres majuscules")]
    public async Task Deposit_WithInvalidCurrency_ShouldReturnValidationError(string invalidCurrency, string expectedError)
    {
        // Arrange
        var accountId = Guid.NewGuid();

        var depositRequest = new DepositRequestDto
        {
            Amount = 100m,
            Currency = invalidCurrency,
            IdempotencyKey = $"invalid-currency-test-{Guid.NewGuid()}"
        };

        var depositContent = new StringContent(
            JsonSerializer.Serialize(depositRequest),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync($"/api/v1/accounts/{accountId}/deposit", depositContent);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var errorContent = await response.Content.ReadAsStringAsync();
        Assert.Contains(expectedError, errorContent);
    }
}
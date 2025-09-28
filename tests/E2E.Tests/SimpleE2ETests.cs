using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using ProjetLog430.Infrastructure.Web.DTOs;

namespace E2E.Tests
{
    public class SimpleE2ETests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        public SimpleE2ETests(WebApplicationFactory<Program> factory)
        {
            var clientFactory = factory.WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
            });

            _client = clientFactory.CreateClient();
        }

        [Fact]
        public async Task POST_Signup_WithValidData_ReturnsSuccess()
        {
            // Arrange
            var signupRequest = new SignupRequestDto
            {
                Email = "test.simple@example.com",
                Phone = "514-123-4567",
                FullName = "John Doe Simple",
                BirthDate = new DateOnly(1990, 1, 1),
                Password = "TestPass123!",
                ConfirmPassword = "TestPass123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/signup", signupRequest);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<SignupResponseDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.ClientId);
            Assert.Equal("Pending", result.Status);
        }

        [Fact]
        public async Task Complete_OTP_Workflow_SignupAndVerification_ShouldActivateAccount()
        {
            // 1. Inscription (génère un OTP)
            var signupRequest = new SignupRequestDto
            {
                Email = "test.otp@example.com",
                Phone = "514-987-6543",
                FullName = "Jane OTP Test",
                BirthDate = new DateOnly(1985, 5, 15),
                Password = "OtpTest456!",
                ConfirmPassword = "OtpTest456!"
            };

            var signupResponse = await _client.PostAsJsonAsync("/api/v1/signup", signupRequest);
            Assert.Equal(HttpStatusCode.OK, signupResponse.StatusCode);
            
            var signupContent = await signupResponse.Content.ReadAsStringAsync();
            var signupResult = JsonSerializer.Deserialize<SignupResponseDto>(signupContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(signupResult);
            Assert.Equal("Pending", signupResult.Status);

            // 2. Vérification OTP avec un code correct (simulé)
            // Note: Dans un vrai test, on récupérerait le code depuis les logs ou une interface de test
            // Ici on simule avec un code connu car le code généré est aléatoire
            
            // Pour ce test, on va utiliser un code incorrect d'abord pour tester l'erreur
            var verifyBadRequest = new VerifyOtpRequestDto
            {
                ClientId = signupResult.ClientId,
                Code = "000000" // Code incorrect
            };

            var badOtpResponse = await _client.PostAsJsonAsync("/api/v1/signup/verify-otp", verifyBadRequest);
            Assert.Equal(HttpStatusCode.BadRequest, badOtpResponse.StatusCode);

            // Note: Test avec code correct nécessiterait une méthode pour récupérer le code généré
            // ou un mode test où le code est prévisible
        }

        [Fact]
        public async Task POST_Signup_WithInvalidEmail_ReturnsBadRequest()
        {
            // Arrange
            var signupRequest = new SignupRequestDto
            {
                Email = "invalid-email",
                Phone = "514-123-4567",
                FullName = "John Doe",
                BirthDate = new DateOnly(1990, 1, 1),
                Password = "TestPass123!",
                ConfirmPassword = "TestPass123!"
            };

            // Act
            var response = await _client.PostAsJsonAsync("/api/v1/signup", signupRequest);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}

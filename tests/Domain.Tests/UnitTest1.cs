using ProjetLog430.Domain.Model.Identite;
using ProjetLog430.Domain.Model.Securite;

namespace Domain.Tests;

public class ClientTests
{
    [Fact]
    public void HashPassword_ValidPassword_ReturnsValidHash()
    {
        // Arrange
        var password = "TestPass123!";

        // Act
        var hash = Client.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.True(hash.Length > 10);
        Assert.StartsWith("$2", hash);
        Assert.NotEqual(password, hash);
    }

    [Fact]
    public void HashPassword_NullPassword_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Client.HashPassword(null!));
    }

    [Fact]
    public void HashPassword_EmptyPassword_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Client.HashPassword(""));
    }

    [Fact]
    public void HashPassword_WhitespacePassword_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => Client.HashPassword("   "));
    }

    [Fact]
    public void Creer_ValidData_CreatesClient()
    {
        // Arrange
        var email = "test@example.com";
        var phone = "1234567890";
        var fullName = "John Doe";
        var password = "TestPass123!";
        var birthDate = new DateOnly(1990, 1, 1);

        // Act
        var passwordHash = Client.HashPassword(password);
        var client = Client.Creer(email, phone, fullName, passwordHash, birthDate);

        // Assert
        Assert.NotNull(client);
        Assert.Equal(email.ToLowerInvariant(), client.Email);
        Assert.Equal(phone, client.Telephone);
        Assert.Equal(fullName, client.NomComplet);
        Assert.Equal(birthDate, client.DateNaissance);
        Assert.Equal(StatutClient.Pending, client.Statut);
        Assert.True(client.VerifyPassword(password));
        Assert.False(client.VerifyPassword("WrongPassword"));
    }

    [Fact]
    public void Creer_InvalidEmail_ThrowsException()
    {
        // Arrange
        var passwordHash = Client.HashPassword("TestPass123!");
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            Client.Creer("invalid-email", null, "John Doe", passwordHash));
    }

    [Fact]
    public void Creer_EmptyFullName_ThrowsException()
    {
        // Arrange
        var passwordHash = Client.HashPassword("TestPass123!");
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            Client.Creer("test@example.com", null, "", passwordHash));
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "TestPass123!";
        var passwordHash = Client.HashPassword(password);
        var client = Client.Creer("test@example.com", null, "John Doe", passwordHash);

        // Act
        var result = client.VerifyPassword(password);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        // Arrange
        var password = "TestPass123!";
        var wrongPassword = "WrongPass456!";
        var passwordHash = Client.HashPassword(password);
        var client = Client.Creer("test@example.com", null, "John Doe", passwordHash);

        // Act
        var result = client.VerifyPassword(wrongPassword);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void VerifyPassword_NullPassword_ReturnsFalse()
    {
        // Arrange
        var password = "TestPass123!";
        var passwordHash = Client.HashPassword(password);
        var client = Client.Creer("test@example.com", null, "John Doe", passwordHash);

        // Act
        var result = client.VerifyPassword(null!);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void UpdatePassword_ValidNewPassword_UpdatesHash()
    {
        // Arrange
        var originalPassword = "Original123!";
        var newPassword = "NewPass456!";
        var passwordHash = Client.HashPassword(originalPassword);
        var client = Client.Creer("test@example.com", null, "John Doe", passwordHash);

        // Act
        client.UpdatePassword(newPassword);

        // Assert
        Assert.False(client.VerifyPassword(originalPassword));
        Assert.True(client.VerifyPassword(newPassword));
    }

    [Theory]
    [InlineData("theodor-george.trif.1@ens.etsmtl.ca")]
    [InlineData("user+tag@example.org")]
    [InlineData("test.email@sub.domain.com")]
    [InlineData("simple@example.com")]
    public void Creer_ComplexEmail_AcceptsValidFormats(string email)
    {
        // Arrange
        var passwordHash = Client.HashPassword("TestPass123!");

        // Act
        var client = Client.Creer(email, null, "Test User", passwordHash);

        // Assert
        Assert.Equal(email.ToLowerInvariant(), client.Email);
    }

    [Fact]
    public void Creer_TelephoneOptional_HandlesNullAndValidValues()
    {
        // Arrange
        var passwordHash = Client.HashPassword("TestPass123!");

        // Act & Assert - Null phone
        var clientNoPhone = Client.Creer("test1@example.com", null, "John Doe", passwordHash);
        Assert.Null(clientNoPhone.Telephone);

        // Act & Assert - Valid phone
        var clientWithPhone = Client.Creer("test2@example.com", "514-123-4567", "Jane Doe", passwordHash);
        Assert.Equal("514-123-4567", clientWithPhone.Telephone);
    }
}

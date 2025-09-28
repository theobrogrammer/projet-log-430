using System.ComponentModel.DataAnnotations;
using ProjetLog430.Infrastructure.Web.DTOs;

namespace Infrastructure.Tests;

public class SignupRequestDtoValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }

    [Fact]
    public void SignupRequestDto_ValidData_PassesValidation()
    {
        // Arrange
        var dto = new SignupRequestDto
        {
            Email = "test@example.com",
            Phone = "514-123-4567",
            FullName = "John Doe",
            BirthDate = new DateOnly(1990, 1, 1),
            Password = "ValidPass123!",
            ConfirmPassword = "ValidPass123!"
        };

        // Act
        var validationResults = ValidateModel(dto);

        // Assert
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void SignupRequestDto_EmptyEmail_FailsValidation(string? email)
    {
        // Arrange
        var dto = new SignupRequestDto
        {
            Email = email!,
            Phone = "514-123-4567",
            FullName = "John Doe",
            BirthDate = new DateOnly(1990, 1, 1),
            Password = "ValidPass123!",
            ConfirmPassword = "ValidPass123!"
        };

        // Act
        var validationResults = ValidateModel(dto);

        // Assert
        Assert.Contains(validationResults, vr => vr.ErrorMessage!.Contains("email est requis"));
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("test@")]
    [InlineData("@domain.com")]
    [InlineData("test..test@domain.com")]
    public void SignupRequestDto_InvalidEmail_FailsValidation(string email)
    {
        // Arrange
        var dto = new SignupRequestDto
        {
            Email = email,
            Phone = "514-123-4567",
            FullName = "John Doe",
            BirthDate = new DateOnly(1990, 1, 1),
            Password = "ValidPass123!",
            ConfirmPassword = "ValidPass123!"
        };

        // Act
        var validationResults = ValidateModel(dto);

        // Assert
        Assert.Contains(validationResults, vr => vr.ErrorMessage!.Contains("Format d'email invalide"));
    }

    [Theory]
    [InlineData("theodor-george.trif.1@ens.etsmtl.ca")]
    [InlineData("user+tag@example.org")]
    [InlineData("test.email@sub.domain.com")]
    [InlineData("simple@example.com")]
    public void SignupRequestDto_ComplexValidEmails_PassValidation(string email)
    {
        // Arrange
        var dto = new SignupRequestDto
        {
            Email = email,
            Phone = "514-123-4567",
            FullName = "John Doe",
            BirthDate = new DateOnly(1990, 1, 1),
            Password = "ValidPass123!",
            ConfirmPassword = "ValidPass123!"
        };

        // Act
        var validationResults = ValidateModel(dto);

        // Assert
        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")]
    public void SignupRequestDto_InvalidFullName_FailsValidation(string fullName)
    {
        // Arrange
        var dto = new SignupRequestDto
        {
            Email = "test@example.com",
            Phone = "514-123-4567",
            FullName = fullName,
            BirthDate = new DateOnly(1990, 1, 1),
            Password = "ValidPass123!",
            ConfirmPassword = "ValidPass123!"
        };

        // Act
        var validationResults = ValidateModel(dto);

        // Assert
        Assert.Contains(validationResults, vr => 
            vr.ErrorMessage!.Contains("nom") || 
            vr.ErrorMessage!.Contains("requis") || 
            vr.ErrorMessage!.Contains("caractères"));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("1234567")]
    [InlineData("")]
    public void SignupRequestDto_TooShortPassword_FailsValidation(string password)
    {
        // Arrange
        var dto = new SignupRequestDto
        {
            Email = "test@example.com",
            Phone = "514-123-4567",
            FullName = "John Doe",
            BirthDate = new DateOnly(1990, 1, 1),
            Password = password,
            ConfirmPassword = password
        };

        // Act
        var validationResults = ValidateModel(dto);

        // Assert
        Assert.Contains(validationResults, vr => 
            vr.ErrorMessage!.Contains("mot de passe") && vr.ErrorMessage!.Contains("8 caractères"));
    }

    [Theory]
    [InlineData("nouppercase123!")]     // Pas de majuscule
    [InlineData("NOLOWERCASE123!")]     // Pas de minuscule
    [InlineData("NoDigitsHere!")]       // Pas de chiffre
    [InlineData("NoSpecialChars123")]   // Pas de caractère spécial
    [InlineData("Simple123")]           // Pas de caractère spécial
    public void SignupRequestDto_WeakPassword_FailsValidation(string password)
    {
        // Arrange
        var dto = new SignupRequestDto
        {
            Email = "test@example.com",
            Phone = "514-123-4567",
            FullName = "John Doe",
            BirthDate = new DateOnly(1990, 1, 1),
            Password = password,
            ConfirmPassword = password
        };

        // Act
        var validationResults = ValidateModel(dto);

        // Assert
        Assert.Contains(validationResults, vr => 
            vr.ErrorMessage!.Contains("minuscule") ||
            vr.ErrorMessage!.Contains("majuscule") ||
            vr.ErrorMessage!.Contains("chiffre") ||
            vr.ErrorMessage!.Contains("caractère spécial"));
    }

    [Theory]
    [InlineData("ValidPass123!", "StrongPass456@")]
    [InlineData("TestPass123!", "")]
    [InlineData("TestPass123!", "DifferentPass456!")]
    public void SignupRequestDto_MismatchedPasswords_FailsValidation(string password, string confirmPassword)
    {
        // Arrange
        var dto = new SignupRequestDto
        {
            Email = "test@example.com",
            Phone = "514-123-4567",
            FullName = "John Doe",
            BirthDate = new DateOnly(1990, 1, 1),
            Password = password,
            ConfirmPassword = confirmPassword
        };

        // Act
        var validationResults = ValidateModel(dto);

        // Assert
        Assert.Contains(validationResults, vr => vr.ErrorMessage!.Contains("confirmation"));
    }

    [Theory]
    [InlineData("StrongPass123!")]
    [InlineData("MySecure456@")]
    [InlineData("aCLin33$")]        // Le mot de passe original de l'utilisateur
    [InlineData("Complex987#")]
    public void SignupRequestDto_StrongPasswords_PassValidation(string password)
    {
        // Arrange
        var dto = new SignupRequestDto
        {
            Email = "test@example.com",
            Phone = "514-123-4567",
            FullName = "John Doe",
            BirthDate = new DateOnly(1990, 1, 1),
            Password = password,
            ConfirmPassword = password
        };

        // Act
        var validationResults = ValidateModel(dto);

        // Assert
        Assert.Empty(validationResults);
    }

    [Fact]
    public void SignupRequestDto_OptionalFields_CanBeNull()
    {
        // Arrange
        var dto = new SignupRequestDto
        {
            Email = "test@example.com",
            Phone = null,          // Optional
            FullName = "John Doe",
            BirthDate = null,      // Optional
            Password = "ValidPass123!",
            ConfirmPassword = "ValidPass123!"
        };

        // Act
        var validationResults = ValidateModel(dto);

        // Assert
        Assert.Empty(validationResults);
    }

    [Fact]
    public void SignupRequestDto_VeryLongValidName_PassesValidation()
    {
        // Arrange
        var longName = new string('A', 99); // 99 caractères (limite: 100)
        var dto = new SignupRequestDto
        {
            Email = "test@example.com",
            Phone = "514-123-4567",
            FullName = longName,
            BirthDate = new DateOnly(1990, 1, 1),
            Password = "ValidPass123!",
            ConfirmPassword = "ValidPass123!"
        };

        // Act
        var validationResults = ValidateModel(dto);

        // Assert
        Assert.Empty(validationResults);
    }

    [Fact]
    public void SignupRequestDto_TooLongName_FailsValidation()
    {
        // Arrange
        var tooLongName = new string('A', 101); // 101 caractères (limite: 100)
        var dto = new SignupRequestDto
        {
            Email = "test@example.com",
            Phone = "514-123-4567",
            FullName = tooLongName,
            BirthDate = new DateOnly(1990, 1, 1),
            Password = "ValidPass123!",
            ConfirmPassword = "ValidPass123!"
        };

        // Act
        var validationResults = ValidateModel(dto);

        // Assert
        Assert.Contains(validationResults, vr => vr.ErrorMessage!.Contains("100 caractères"));
    }
}

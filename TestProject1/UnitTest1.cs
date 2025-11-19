using PROG6212_ST10449143_POE_PART_1.Models;
using PROG6212_ST10449143_POE_PART_1.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Xunit;

namespace PROG6212_ST10449143_POE_PART_1.Tests
{
    public class UnitTest1
    {
        [Fact]
        public void CalculateTotalAmount_ValidInputs_ReturnsCorrectTotal()
        {
            // Arrange
            var claim = new Models.Claim
            {
                LecturerName = "Dr. John Smith",
                Month = "January",
                HoursWorked = 40.5m,
                HourlyRate = 250.75m
            };

            // Act
            var expectedTotal = 40.5m * 250.75m;
            var actualTotal = claim.TotalAmount;

            // Assert
            Assert.Equal(expectedTotal, actualTotal);
            Assert.Equal(10155.375m, actualTotal);
        }

        [Fact]
        public void AdditionalNotes_NullAndEmpty()
        {
            // Arrange & Act - Test constructor handles null
            var claim = new Models.Claim
            {
                LecturerName = "Dr. Test User",
                Month = "January",
                HoursWorked = 40m,
                HourlyRate = 200m
            };

            // Assert - Constructor should initialize to empty string
            Assert.NotNull(claim.AdditionalNotes);
            Assert.Equal(string.Empty, claim.AdditionalNotes);

            // Test explicit empty string
            var claimWithEmpty = new Models.Claim
            {
                LecturerName = "Dr. Test User",
                Month = "January",
                HoursWorked = 40m,
                HourlyRate = 200m,
                AdditionalNotes = ""
            };

            Assert.Equal(string.Empty, claimWithEmpty.AdditionalNotes);
        }

        private readonly DocumentValidator _validator = new DocumentValidator();

        [Fact]
        public void ValidateDocument_ValidFiles_ReturnsSuccess()
        {
            // Test Case 1: Valid PDF file
            var result1 = _validator.ValidateDocument("document.pdf", 1024 * 1024);
            Assert.True(result1.IsValid);
            Assert.Empty(result1.Errors);

            // Test Case 2: Valid image file
            var result2 = _validator.ValidateDocument("photo.jpg", 2 * 1024 * 1024);
            Assert.True(result2.IsValid);
            Assert.Empty(result2.Errors);

            // Test Case 3: Valid document file
            var result3 = _validator.ValidateDocument("contract.docx", 3 * 1024 * 1024);
            Assert.True(result3.IsValid);
            Assert.Empty(result3.Errors);
        }
    }
}
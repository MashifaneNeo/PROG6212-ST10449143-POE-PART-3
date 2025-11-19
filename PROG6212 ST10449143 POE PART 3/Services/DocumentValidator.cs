using System.ComponentModel.DataAnnotations;

namespace PROG6212_ST10449143_POE_PART_1.Services
{
    public class DocumentValidator
    {
        private readonly string[] _allowedExtensions = { ".pdf", ".docx", ".xlsx", ".jpg", ".png", ".jpeg" };
        private readonly long _maxFileSize = 5 * 1024 * 1024; 

        public DocumentValidationResult ValidateDocument(string fileName, long fileSize)
        {
            var result = new DocumentValidationResult();

            // 1. Check if file has a name
            if (string.IsNullOrWhiteSpace(fileName))
            {
                result.AddError("File must have a name");
                return result;
            }

            // 2. Check file extension
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (!_allowedExtensions.Contains(extension))
            {
                result.AddError($"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", _allowedExtensions)}");
                return result;
            }

            // 3. Check file size
            if (fileSize > _maxFileSize)
            {
                result.AddError($"File size {fileSize} bytes exceeds maximum allowed size of {_maxFileSize} bytes (5MB)");
                return result;
            }

            // 4. Check for dangerous file names
            if (fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            {
                result.AddError("File name contains invalid characters");
                return result;
            }

            result.IsValid = true;
            return result;
        }

        public string GenerateSafeFileName(string originalFileName)
        {
            var extension = Path.GetExtension(originalFileName);
            var safeName = $"{Guid.NewGuid()}{extension}";
            return $"{safeName}|{Path.GetFileName(originalFileName)}";
        }
    }

    public class DocumentValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public string ErrorMessage => string.Join("; ", Errors);

        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }
    }
}
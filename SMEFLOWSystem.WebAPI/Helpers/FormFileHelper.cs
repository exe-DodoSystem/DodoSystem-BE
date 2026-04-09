namespace SMEFLOWSystem.WebAPI.Helpers;

/// <summary>
/// Utility for converting uploaded files (IFormFile) to other formats.
/// Lives in WebAPI layer because IFormFile is an ASP.NET Core concern.
/// </summary>
public static class FormFileHelper
{
    /// <summary>
    /// Convert IFormFile to base64 data URI string.
    /// Example output: "data:image/jpeg;base64,/9j/4AAQ..."
    /// </summary>
    public static async Task<string?> ToBase64DataUriAsync(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return null;

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        var contentType = file.ContentType ?? "image/jpeg";
        return $"data:{contentType};base64,{base64}";
    }
}

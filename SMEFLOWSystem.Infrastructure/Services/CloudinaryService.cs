using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Config;

namespace SMEFLOWSystem.Infrastructure.Services;

public class CloudinaryService : ICloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IOptions<CloudinarySettings> settings)
    {
        var cfg = settings.Value;

        if (string.IsNullOrWhiteSpace(cfg.CloudName))
            throw new InvalidOperationException("Missing config: Cloudinary:CloudName");
        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
            throw new InvalidOperationException("Missing config: Cloudinary:ApiKey");
        if (string.IsNullOrWhiteSpace(cfg.ApiSecret))
            throw new InvalidOperationException("Missing config: Cloudinary:ApiSecret");

        var account = new Account(cfg.CloudName, cfg.ApiKey, cfg.ApiSecret);
        _cloudinary = new Cloudinary(account) { Api = { Secure = true } };
    }

    public async Task<string> UploadBase64Async(string base64Image, string folder)
    {
        // Strip data URI prefix nếu có: "data:image/jpeg;base64,..."
        var base64Data = base64Image.Contains(',')
            ? base64Image.Split(',')[1]
            : base64Image;

        var bytes = Convert.FromBase64String(base64Data);
        using var stream = new MemoryStream(bytes);

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription($"{Guid.NewGuid()}.jpg", stream),
            Folder = folder,
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

        return result.SecureUrl.ToString();
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string folder)
    {
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, fileStream),
            Folder = folder,
            UseFilename = false,
            UniqueFilename = true,
            Overwrite = false
        };

        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
            throw new InvalidOperationException($"Cloudinary upload failed: {result.Error.Message}");

        return result.SecureUrl.ToString();
    }

    public async Task DeleteAsync(string publicId)
    {
        var deleteParams = new DeletionParams(publicId);
        await _cloudinary.DestroyAsync(deleteParams);
    }
}

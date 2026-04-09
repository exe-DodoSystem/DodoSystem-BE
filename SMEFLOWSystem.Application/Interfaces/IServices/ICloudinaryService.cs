namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface ICloudinaryService
{
    /// <summary>
    /// Upload ảnh từ Base64 string lên Cloudinary.
    /// </summary>
    /// <param name="base64Image">Chuỗi base64 của ảnh (có thể có prefix "data:image/jpeg;base64,")</param>
    /// <param name="folder">Folder trên Cloudinary (e.g. "attendance/checkin")</param>
    /// <returns>URL của ảnh đã upload</returns>
    Task<string> UploadBase64Async(string base64Image, string folder);

    /// <summary>
    /// Upload ảnh từ IFormFile lên Cloudinary.
    /// </summary>
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string folder);

    /// <summary>
    /// Xóa ảnh trên Cloudinary theo publicId.
    /// </summary>
    Task DeleteAsync(string publicId);
}

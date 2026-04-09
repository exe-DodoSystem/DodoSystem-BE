using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Config;
using System.Text.Json;

namespace SMEFLOWSystem.Infrastructure.Services;

public class FacePlusPlusVerificationService : IFaceVerificationService
{
    private readonly FacePlusPlusSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<FacePlusPlusVerificationService> _logger;

    public FacePlusPlusVerificationService(
        IOptions<FacePlusPlusSettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<FacePlusPlusVerificationService> logger)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        // Fail-fast nếu thiếu config → phát hiện ngay lúc khởi động
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || _settings.ApiKey == "SET-IN-USER-SECRETS")
            throw new InvalidOperationException("Missing config: FacePlusPlus:ApiKey");
        if (string.IsNullOrWhiteSpace(_settings.ApiSecret) || _settings.ApiSecret == "SET-IN-USER-SECRETS")
            throw new InvalidOperationException("Missing config: FacePlusPlus:ApiSecret");

        _logger.LogInformation("Face++ initialized — ApiKey length: {Len}", _settings.ApiKey.Length);
    }

    public async Task<FaceVerificationResult> VerifyAsync(string selfieUrl, string avatarUrl)
    {
        var httpClient = _httpClientFactory.CreateClient("FacePlusPlus");

        var formData = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["api_key"] = _settings.ApiKey,
            ["api_secret"] = _settings.ApiSecret,
            ["image_url1"] = selfieUrl,
            ["image_url2"] = avatarUrl
        });

        var url = _settings.BaseUrl.TrimEnd('/') + "/facepp/v3/compare";

        _logger.LogInformation("Face++ compare: image_url1={SelfieUrl}, image_url2={AvatarUrl}", selfieUrl, avatarUrl);

        var response = await httpClient.PostAsync(url, formData);
        var json = await response.Content.ReadAsStringAsync();

        _logger.LogInformation("Face++ response: {StatusCode} — {Body}", response.StatusCode, json);

        // Face++ trả 200 kể cả khi có lỗi logic, nên parse JSON trước
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Check error_message
        if (root.TryGetProperty("error_message", out var errProp))
        {
            var errMsg = errProp.GetString() ?? "Unknown error";

            if (errMsg.Contains("NO_FACE_FOUND", StringComparison.OrdinalIgnoreCase))
                return new FaceVerificationResult(false, 0, "Không phát hiện khuôn mặt trong ảnh.");

            throw new InvalidOperationException($"Face++ compare failed: {errMsg}");
        }

        // Parse confidence (Face++ trả 0-100)
        if (!root.TryGetProperty("confidence", out var confidenceProp))
            return new FaceVerificationResult(false, 0, "Không phát hiện khuôn mặt trong ảnh.");

        var confidence = confidenceProp.GetDouble();
        var isMatch = confidence >= _settings.ConfidenceThreshold;

        return new FaceVerificationResult(
            IsMatch: isMatch,
            Confidence: confidence / 100.0,   // normalize về 0-1 đúng contract interface
            ErrorMessage: null
        );
    }
}
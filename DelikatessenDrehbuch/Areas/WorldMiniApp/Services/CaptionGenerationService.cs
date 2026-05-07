using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace DelikatessenDrehbuch.Areas.WorldMiniApp.Services
{
    /// <summary>
    /// Generates German WebVTT captions via Whisper API and translates them to the app languages via OpenAI text models.
    /// </summary>
    public class CaptionGenerationService
    {
        private static readonly string[] DefaultTargetLanguages =
        [
            "en",
            "es",
            "pt",
            "id",
            "nl",
            "sv",
            "da",
            "nb",
            "ms"
        ];

        private static readonly IReadOnlyDictionary<string, string> LanguageNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["de"] = "German",
            ["en"] = "English",
            ["es"] = "Spanish",
            ["pt"] = "Portuguese",
            ["id"] = "Indonesian",
            ["nl"] = "Dutch",
            ["sv"] = "Swedish",
            ["da"] = "Danish",
            ["nb"] = "Norwegian Bokmal",
            ["ms"] = "Malay"
        };

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<CaptionGenerationService> _logger;

        public CaptionGenerationService(
            IHttpClientFactory httpClientFactory,
            IConfiguration config,
            ILogger<CaptionGenerationService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _logger = logger;
        }

        public async Task<bool> GenerateCaptionsForVideoAsync(string videoUrl, string videoGuid)
        {
            try
            {
                _logger.LogWarning("Caption generation started. VideoGuid={VideoGuid} VideoUrl={VideoUrl}", videoGuid, videoUrl);

                var videoPath = await DownloadVideoAsync(videoUrl, videoGuid);
                if (string.IsNullOrWhiteSpace(videoPath))
                {
                    _logger.LogError("Caption generation aborted: downloadable video could not be resolved. VideoGuid={VideoGuid}", videoGuid);
                    return false;
                }

                try
                {
                    var germanVtt = await TranscribeWithWhisperAsync(videoPath);
                    if (string.IsNullOrWhiteSpace(germanVtt))
                    {
                        _logger.LogError("Whisper transcription returned no VTT. VideoGuid={VideoGuid}", videoGuid);
                        return false;
                    }

                    await UploadCaptionToBunnyAsync(videoGuid, "de", germanVtt);
                    _logger.LogWarning("German caption uploaded successfully. VideoGuid={VideoGuid}", videoGuid);

                    foreach (var targetLanguage in GetTargetLanguages())
                    {
                        if (targetLanguage.Equals("de", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        try
                        {
                            var translatedVtt = await TranslateVttWithOpenAiAsync(germanVtt, targetLanguage);
                            if (string.IsNullOrWhiteSpace(translatedVtt))
                            {
                                _logger.LogWarning("Translated VTT was empty. Language={Language} VideoGuid={VideoGuid}", targetLanguage, videoGuid);
                                continue;
                            }

                            if (!IsMeaningfullyTranslated(germanVtt, translatedVtt, targetLanguage))
                            {
                                _logger.LogWarning("Skipping untranslated caption upload because target output matched German source. Language={Language} VideoGuid={VideoGuid}", targetLanguage, videoGuid);
                                continue;
                            }

                            await UploadCaptionToBunnyAsync(videoGuid, targetLanguage, translatedVtt);
                            _logger.LogWarning("Translated caption uploaded successfully. Language={Language} VideoGuid={VideoGuid}", targetLanguage, videoGuid);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Caption translation failed. Language={Language} VideoGuid={VideoGuid}", targetLanguage, videoGuid);
                        }
                    }

                    return true;
                }
                finally
                {
                    if (File.Exists(videoPath))
                    {
                        File.Delete(videoPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Caption generation failed. VideoGuid={VideoGuid}", videoGuid);
                return false;
            }
        }

        private async Task<string?> DownloadVideoAsync(string videoUrl, string videoGuid)
        {
            try
            {
                var candidateUrls = BuildVideoDownloadCandidates(videoUrl).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (candidateUrls.Count == 0)
                {
                    return null;
                }

                var client = _httpClientFactory.CreateClient();
                foreach (var candidateUrl in candidateUrls)
                {
                    try
                    {
                        using var response = await client.GetAsync(candidateUrl, HttpCompletionOption.ResponseHeadersRead);
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Video download candidate failed. StatusCode={StatusCode} Url={Url}", response.StatusCode, candidateUrl);
                            continue;
                        }

                        var tempPath = Path.Combine(Path.GetTempPath(), $"{videoGuid}.mp4");
                        await using var fileStream = File.Create(tempPath);
                        await response.Content.CopyToAsync(fileStream);
                        _logger.LogWarning("Video downloaded for captions. Url={Url} Size={Size}", candidateUrl, new FileInfo(tempPath).Length);
                        return tempPath;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Video download candidate threw. Url={Url}", candidateUrl);
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download video for caption generation. VideoUrl={VideoUrl}", videoUrl);
                return null;
            }
        }

        private static IEnumerable<string> BuildVideoDownloadCandidates(string videoUrl)
        {
            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                yield break;
            }

            if (Uri.TryCreate(videoUrl, UriKind.Absolute, out var uri)
                && uri.AbsolutePath.EndsWith("/playlist.m3u8", StringComparison.OrdinalIgnoreCase))
            {
                var baseUrl = uri.AbsoluteUri.Substring(0, uri.AbsoluteUri.Length - "/playlist.m3u8".Length);
                yield return $"{baseUrl}/original";
                yield return $"{baseUrl}/play_1080p.mp4";
                yield return $"{baseUrl}/play_720p.mp4";
                yield return $"{baseUrl}/play_480p.mp4";
                yield return $"{baseUrl}/play_360p.mp4";
                yield return $"{baseUrl}/play_240p.mp4";
            }

            yield return videoUrl;
        }

        private async Task<string?> TranscribeWithWhisperAsync(string videoPath)
        {
            try
            {
                var apiKey = GetOpenAiApiKey();

                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogError("OPENAI_API_KEY is not configured.");
                    return null;
                }

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using var content = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(await File.ReadAllBytesAsync(videoPath));
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                content.Add(fileContent, "file", Path.GetFileName(videoPath));
                content.Add(new StringContent("whisper-1"), "model");
                content.Add(new StringContent("vtt"), "response_format");
                content.Add(new StringContent("de"), "language");

                using var response = await client.PostAsync("https://api.openai.com/v1/audio/transcriptions", content);
                response.EnsureSuccessStatusCode();

                var vttContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Whisper transcription completed. Characters={Length}", vttContent.Length);
                return vttContent;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Whisper transcription failed.");
                return null;
            }
        }

        private async Task<string?> TranslateVttWithOpenAiAsync(string sourceVtt, string targetLanguage)
        {
            var apiKey = GetOpenAiApiKey();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogError("OPENAI_API_KEY is not configured.");
                return null;
            }

            var targetLanguageName = LanguageNames.TryGetValue(targetLanguage, out var languageName)
                ? languageName
                : targetLanguage;
            var model = _config["OpenAI:CaptionTranslationModel"]
                ?? _config["OPENAI_CAPTION_TRANSLATION_MODEL"]
                ?? "gpt-4o-mini";

            var systemPrompt = $"""
Translate this WebVTT subtitle file from German into {targetLanguageName}.

Rules:
- Return only valid WebVTT content.
- Keep the WEBVTT header.
- Keep cue timings, cue settings, numbering, and blank-line structure unchanged.
- Translate only the spoken caption text.
- Every spoken caption line must be translated out of German unless it is a proper noun, brand name, or universally unchanged term.
- Do not add comments, explanations, or markdown.
""";

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = sourceVtt }
                },
                temperature = 0.3
            };

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("https://api.openai.com/v1/chat/completions", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseJson);

            if (!document.RootElement.TryGetProperty("choices", out var choicesElement)
                || choicesElement.GetArrayLength() == 0)
            {
                _logger.LogWarning("OpenAI chat completion returned no choices for subtitle translation. Language={Language}", targetLanguage);
                return null;
            }

            var translated = choicesElement[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(translated))
            {
                return null;
            }

            return NormalizeWebVtt(translated);
        }

        private string[] GetTargetLanguages()
        {
            var configured = _config["OpenAI:CaptionTargetLanguages"] ?? _config["OPENAI_CAPTION_TARGET_LANGUAGES"];
            if (string.IsNullOrWhiteSpace(configured))
            {
                return DefaultTargetLanguages;
            }

            return configured
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private string? GetOpenAiApiKey()
        {
            return Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? _config["SecretKeyOpenAi"]
                ?? _config["OpenAI:ApiKey"]
                ?? _config["OPENAI_API_KEY"];
        }

        private static string NormalizeWebVtt(string content)
        {
            var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            if (!normalized.StartsWith("WEBVTT", StringComparison.OrdinalIgnoreCase))
            {
                normalized = $"WEBVTT\n\n{normalized}";
            }

            return normalized.Replace("\n", Environment.NewLine) + Environment.NewLine;
        }

        private static bool IsMeaningfullyTranslated(string sourceVtt, string translatedVtt, string targetLanguage)
        {
            if (targetLanguage.Equals("de", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.Equals(
                NormalizeWebVtt(sourceVtt),
                NormalizeWebVtt(translatedVtt),
                StringComparison.Ordinal);
        }

        private async Task UploadCaptionToBunnyAsync(string videoGuid, string language, string vttContent)
        {
            try
            {
                var storageAddress = _config["Bunny_Net_Storage_Adres"];
                var apiKey = _config["Bunny_Net_Passwort_Lager"];

                if (string.IsNullOrWhiteSpace(storageAddress) || string.IsNullOrWhiteSpace(apiKey))
                {
                    _logger.LogError("Bunny storage credentials are missing for caption upload.");
                    return;
                }

                var uploadPath = $"captions/{videoGuid}/{language.ToLowerInvariant()}.vtt";
                var url = $"{storageAddress.TrimEnd('/')}/{uploadPath}";

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("AccessKey", apiKey);

                using var content = new StringContent(vttContent, Encoding.UTF8, "text/vtt");
                using var response = await client.PutAsync(url, content);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Caption upload failed for {Language}. VideoGuid={VideoGuid}", language, videoGuid);
            }
        }
    }
}

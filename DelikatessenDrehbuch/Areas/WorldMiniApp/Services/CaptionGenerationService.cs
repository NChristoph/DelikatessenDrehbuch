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
                _logger.LogInformation("Caption generation started. VideoGuid={VideoGuid}", videoGuid);

                // If a German caption already exists, captions were already generated — either the
                // creator pasted a VTT at upload time (translations done there) or a prior webhook run.
                // Nothing to do.
                var existingGerman = await TryDownloadManualSourceCaptionAsync(videoGuid);
                if (!string.IsNullOrWhiteSpace(existingGerman))
                {
                    _logger.LogInformation("German caption already present; skipping Whisper/translation. VideoGuid={VideoGuid}", videoGuid);
                    return true;
                }

                // No caption yet → transcribe with Whisper, then translate into all languages.
                var videoPath = await DownloadVideoAsync(videoUrl, videoGuid);
                if (string.IsNullOrWhiteSpace(videoPath))
                {
                    _logger.LogError("Caption generation aborted: downloadable video could not be resolved. VideoGuid={VideoGuid}", videoGuid);
                    return false;
                }

                string? germanVtt;
                try
                {
                    germanVtt = await TranscribeWithWhisperAsync(videoPath);
                }
                finally
                {
                    if (File.Exists(videoPath))
                    {
                        File.Delete(videoPath);
                    }
                }

                if (string.IsNullOrWhiteSpace(germanVtt))
                {
                    _logger.LogError("No source VTT available (Whisper returned nothing). VideoGuid={VideoGuid}", videoGuid);
                    return false;
                }

                await UploadGermanAndTranslateAsync(videoGuid, germanVtt);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Caption generation failed. VideoGuid={VideoGuid}", videoGuid);
                return false;
            }
        }

        /// <summary>
        /// Uploads the German source caption (de.vtt) and generates + uploads translated caption tracks
        /// for every other language. Shared by the Whisper webhook path and the upload-time path.
        /// </summary>
        public async Task<(int Succeeded, int Total)> UploadGermanAndTranslateAsync(string videoGuid, string germanVtt)
        {
            germanVtt = NormalizeWebVtt(germanVtt);

            await UploadCaptionToBunnyAsync(videoGuid, "de", germanVtt);
            _logger.LogWarning("German caption uploaded successfully. VideoGuid={VideoGuid}", videoGuid);

            var targetLanguages = GetTargetLanguages()
                .Where(l => !l.Equals("de", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            _logger.LogWarning("Caption translation starting for {Count} languages [{Languages}]. VideoGuid={VideoGuid}",
                targetLanguages.Length, string.Join(",", targetLanguages), videoGuid);

            var succeeded = 0;
            foreach (var targetLanguage in targetLanguages)
            {
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
                    succeeded++;
                    _logger.LogWarning("Translated caption uploaded successfully. Language={Language} VideoGuid={VideoGuid}", targetLanguage, videoGuid);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Caption translation failed. Language={Language} VideoGuid={VideoGuid}", targetLanguage, videoGuid);
                }
            }

            _logger.LogWarning("Caption translations done: {Succeeded}/{Total} uploaded. VideoGuid={VideoGuid}",
                succeeded, targetLanguages.Length, videoGuid);

            return (succeeded, targetLanguages.Length);
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
                        return tempPath;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Video download candidate failed.");
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download video for caption generation.");
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

                // Source language for transcription. Defaults to German (the app is German-first and
                // the downstream translation step assumes a German source VTT). Set
                // "OpenAI:WhisperLanguage" to another ISO code, or to "auto"/empty to let Whisper
                // auto-detect. NOTE: if you switch away from "de", the "translate from German" step
                // in TranslateVttWithOpenAiAsync must be revisited.
                var whisperLanguage = (_config["OpenAI:WhisperLanguage"] ?? "de").Trim();
                if (!string.IsNullOrWhiteSpace(whisperLanguage) &&
                    !string.Equals(whisperLanguage, "auto", StringComparison.OrdinalIgnoreCase))
                {
                    content.Add(new StringContent(whisperLanguage), "language");
                }

                using var response = await client.PostAsync("https://api.openai.com/v1/audio/transcriptions", content);
                response.EnsureSuccessStatusCode();

                var vttContent = await response.Content.ReadAsStringAsync();
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
            // Use the SAME keys as the other (working) OpenAI services first. The OPENAI_API_KEY
            // environment variable is checked LAST — a stale/invalid one there was causing 401s on
            // caption translation while recipe/ingredient calls (which read the config keys) worked.
            return FirstNonBlank(
                Environment.GetEnvironmentVariable("SecretKeyOpenAi"),
                _config["SecretKeyOpenAi"],
                _config["OpenAI:ApiKey"],
                Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
                _config["OPENAI_API_KEY"]);
        }

        private static string? FirstNonBlank(params string?[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v))
                {
                    return v;
                }
            }
            return null;
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

        // A creator-provided source VTT is stored directly as the German caption (de.vtt) — the same
        // naming the player and translation use. That makes the German track available immediately
        // (even if the caption webhook never fires) and lets GenerateCaptionsForVideoAsync pick it up
        // as the translation source instead of running Whisper.
        private static string ManualSourceCaptionPath(string videoGuid) => $"captions/{videoGuid}/de.vtt";

        /// <summary>
        /// Generates all caption tracks (de.vtt + translations) from a creator-pasted source VTT
        /// (assumed German). Called from the upload background task so captions/translations do NOT
        /// depend on the Bunny webhook firing or the recipe save succeeding.
        /// </summary>
        public async Task<bool> GenerateCaptionsFromSourceVttAsync(string videoGuid, string vttContent)
        {
            if (string.IsNullOrWhiteSpace(videoGuid) || string.IsNullOrWhiteSpace(vttContent))
            {
                return false;
            }

            try
            {
                await UploadGermanAndTranslateAsync(videoGuid, vttContent);
                _logger.LogInformation("Generated captions + translations from creator-pasted VTT. VideoGuid={VideoGuid}", videoGuid);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate captions from creator-pasted VTT. VideoGuid={VideoGuid}", videoGuid);
                return false;
            }
        }

        /// <summary>
        /// Re-generates the translated caption tracks for a video that already has a German caption
        /// (de.vtt) in Bunny — used to repair videos whose translations failed (e.g. a bad API key).
        /// Reads the existing de.vtt and re-runs the translation loop.
        /// </summary>
        public async Task<(bool Found, int Succeeded, int Total)> RegenerateTranslationsAsync(string videoGuid)
        {
            var germanVtt = await TryDownloadManualSourceCaptionAsync(videoGuid);
            if (string.IsNullOrWhiteSpace(germanVtt))
            {
                _logger.LogWarning("RegenerateTranslations: no existing de.vtt found. VideoGuid={VideoGuid}", videoGuid);
                return (false, 0, 0);
            }

            var (succeeded, total) = await UploadGermanAndTranslateAsync(videoGuid, germanVtt);
            return (true, succeeded, total);
        }

        private async Task<string?> TryDownloadManualSourceCaptionAsync(string videoGuid)
        {
            try
            {
                var storageAddress = _config["Bunny_Net_Storage_Adres"];
                var apiKey = _config["Bunny_Net_Passwort_Lager"];
                if (string.IsNullOrWhiteSpace(storageAddress) || string.IsNullOrWhiteSpace(apiKey))
                {
                    return null;
                }

                var url = $"{storageAddress.TrimEnd('/')}/{ManualSourceCaptionPath(videoGuid)}";
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("AccessKey", apiKey);

                using var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    // 404 = no manual VTT for this video (the normal case) → fall back to Whisper.
                    return null;
                }

                var vtt = await response.Content.ReadAsStringAsync();
                return string.IsNullOrWhiteSpace(vtt) ? null : vtt;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check for manual source VTT (falling back to Whisper). VideoGuid={VideoGuid}", videoGuid);
                return null;
            }
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

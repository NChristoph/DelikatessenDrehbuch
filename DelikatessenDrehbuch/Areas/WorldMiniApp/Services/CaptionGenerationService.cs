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
                _logger.LogWarning("Caption download: trying {Count} candidate URL(s) for VideoGuid={VideoGuid}. Source={Source}. Candidates=[{Candidates}]",
                    candidateUrls.Count, videoGuid, videoUrl, string.Join(" | ", candidateUrls));
                if (candidateUrls.Count == 0)
                {
                    return null;
                }

                var client = _httpClientFactory.CreateClient();
                var tooLarge = false;
                foreach (var candidateUrl in candidateUrls)
                {
                    try
                    {
                        using var response = await client.GetAsync(candidateUrl, HttpCompletionOption.ResponseHeadersRead);
                        if (!response.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Caption download candidate FAILED. StatusCode={StatusCode} Url={Url}", (int)response.StatusCode, candidateUrl);
                            continue;
                        }

                        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                        var contentLength = response.Content.Headers.ContentLength ?? -1;
                        // Eine m3u8-Playlist ist KEIN Video (Whisper kann sie nicht nutzen) → überspringen.
                        if (candidateUrl.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
                            || contentType.Contains("mpegurl", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogWarning("Caption download candidate is an HLS playlist, not a video file — skipping. Url={Url}", candidateUrl);
                            continue;
                        }

                        // OpenAI Whisper: hartes 25-MB-Limit. Vorab per Header überspringen, wenn bekannt.
                        const long whisperMaxBytes = 24_500_000;
                        if (contentLength > whisperMaxBytes)
                        {
                            _logger.LogWarning("Caption download candidate too large for Whisper (header {Bytes} bytes) — trying a smaller rendition. Url={Url}", contentLength, candidateUrl);
                            tooLarge = true;
                            continue;
                        }

                        var tempPath = Path.Combine(Path.GetTempPath(), $"{videoGuid}.mp4");
                        await using (var fileStream = File.Create(tempPath))
                        {
                            await response.Content.CopyToAsync(fileStream);
                        }

                        // Echte Größe prüfen (Content-Length-Header fehlt bei Bunny oft → chunked).
                        var actualBytes = new FileInfo(tempPath).Length;
                        if (actualBytes > whisperMaxBytes)
                        {
                            _logger.LogWarning("Caption download too large for Whisper: {Bytes} bytes (>25 MB) at {Url} — skipping.", actualBytes, candidateUrl);
                            try { File.Delete(tempPath); } catch { /* best effort */ }
                            tooLarge = true;
                            continue;
                        }

                        _logger.LogWarning("Caption download OK for Whisper. Url={Url} ContentType={ContentType} Bytes={Bytes}", candidateUrl, contentType, actualBytes);
                        return tempPath;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Caption download candidate threw. Url={Url}", candidateUrl);
                    }
                }

                if (tooLarge)
                {
                    _logger.LogError("Caption download: alle verfügbaren Renditions sind >25 MB (Whisper-Limit). VideoGuid={VideoGuid}. Das Video ist zu lang/hochauflösend — es fehlt eine kleine Rendition (z.B. play_240p.mp4). In Bunny Stream MP4-Fallback mit niedriger Auflösung aktivieren/re-encodieren.",
                        videoGuid);
                }
                else
                {
                    _logger.LogError("Caption download: ALL candidates failed for VideoGuid={VideoGuid}. Source={Source}. Prüfe in Bunny Stream: MP4-Fallback aktiv? Token-Authentifizierung aus? Video fertig transcodiert?",
                        videoGuid, videoUrl);
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
                // KLEINSTE Rendition zuerst: Whisper braucht nur die Tonspur, und die OpenAI-Whisper-API
                // hat ein hartes 25-MB-Limit. Original/1080p sprengen das schnell (Verbindung bricht ab),
                // 240p hat dieselbe Audiospur bei Bruchteil der Größe → bewusst KEIN original/1080p.
                yield return $"{baseUrl}/play_240p.mp4";
                yield return $"{baseUrl}/play_360p.mp4";
                yield return $"{baseUrl}/play_480p.mp4";
                yield return $"{baseUrl}/play_720p.mp4";
                yield break;
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
                // "Expect: 100-continue" beim Upload abschalten — dieser Header ist die häufigste Ursache
                // für einen Verbindungsabbruch (SocketException 10054) mitten im Datei-Upload zu OpenAI
                // (Proxy/AV/Server verwerfen ihn). Der Body wird dann direkt gesendet.
                client.DefaultRequestHeaders.ExpectContinue = false;
                client.Timeout = TimeSpan.FromMinutes(5);

                var videoBytes = await File.ReadAllBytesAsync(videoPath);
                var whisperLanguage = (_config["OpenAI:WhisperLanguage"] ?? "de").Trim();

                const int maxAttempts = 3;
                for (var attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        using var content = new MultipartFormDataContent();
                        var fileContent = new ByteArrayContent(videoBytes);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                        content.Add(fileContent, "file", Path.GetFileName(videoPath));
                        content.Add(new StringContent("whisper-1"), "model");
                        content.Add(new StringContent("vtt"), "response_format");

                        // Source language for transcription. Defaults to German (the app is German-first
                        // and the downstream translation step assumes a German source VTT). Set
                        // "OpenAI:WhisperLanguage" to another ISO code, or to "auto"/empty to auto-detect.
                        if (!string.IsNullOrWhiteSpace(whisperLanguage) &&
                            !string.Equals(whisperLanguage, "auto", StringComparison.OrdinalIgnoreCase))
                        {
                            content.Add(new StringContent(whisperLanguage), "language");
                        }

                        // Erzwinge HTTP/1.1 (HTTP/2-Multipart-Uploads werden von manchen Netzen/Proxys gekappt).
                        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions")
                        {
                            Content = content,
                            Version = System.Net.HttpVersion.Version11,
                            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
                        };

                        _logger.LogWarning("Whisper request attempt {Attempt}/{Max}: sending {Bytes} bytes.", attempt, maxAttempts, videoBytes.Length);
                        using var response = await client.SendAsync(request);
                        if (!response.IsSuccessStatusCode)
                        {
                            var err = await response.Content.ReadAsStringAsync();
                            _logger.LogError("Whisper HTTP {Status}: {Body}", (int)response.StatusCode, Truncate(err, 400));
                            response.EnsureSuccessStatusCode();
                        }

                        return await response.Content.ReadAsStringAsync();
                    }
                    catch (Exception ex) when (attempt < maxAttempts)
                    {
                        _logger.LogWarning(ex, "Whisper attempt {Attempt}/{Max} failed — retrying.", attempt, maxAttempts);
                        await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Whisper transcription failed.");
                return null;
            }
        }

        /// <summary>
        /// Sendet einen JSON-POST an OpenAI mit gehärtetem Transport: „Expect: 100-continue" aus und
        /// HTTP/1.1 erzwungen (beides häufige Ursachen für Verbindungsabbrüche beim Upload hinter
        /// Proxys/AV), plus Retry. Wird von den Caption-Übersetzungen genutzt.
        /// </summary>
        private async Task<HttpResponseMessage> SendOpenAiJsonWithRetryAsync(string url, string jsonBody, string apiKey, int maxAttempts = 3)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var client = _httpClientFactory.CreateClient();
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    client.DefaultRequestHeaders.ExpectContinue = false;
                    client.Timeout = TimeSpan.FromMinutes(3);

                    var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(jsonBody, Encoding.UTF8, "application/json"),
                        Version = System.Net.HttpVersion.Version11,
                        VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
                    };
                    return await client.SendAsync(request);
                }
                catch (Exception ex) when (attempt < maxAttempts)
                {
                    _logger.LogWarning(ex, "OpenAI POST attempt {Attempt}/{Max} failed — retrying. Url={Url}", attempt, maxAttempts, url);
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt));
                }
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

            using var response = await SendOpenAiJsonWithRetryAsync(
                "https://api.openai.com/v1/chat/completions",
                JsonSerializer.Serialize(requestBody),
                apiKey);
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

        private static string Truncate(string? value, int max) =>
            string.IsNullOrEmpty(value) || value.Length <= max ? (value ?? string.Empty) : value.Substring(0, max);

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
        /// <summary>Returns the current German caption (de.vtt) for a video, or null if none exists.</summary>
        public Task<string?> GetGermanCaptionAsync(string videoGuid) => TryDownloadManualSourceCaptionAsync(videoGuid);

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

using ElsaMina.Core;
using ElsaMina.Core.Services.Clock;
using ElsaMina.Core.Services.Config;
using ElsaMina.Core.Services.Http;
using ElsaMina.FileSharing;
using ElsaMina.Logging;

namespace ElsaMina.Commands.Ai.TextToSpeech;

public class ElevenLabsAiTextToSpeechProvider : IAiTextToSpeechProvider
{
    private const string FEMALE_VOICE_ID = "EXAVITQu4vr4xnSDxMaL";
    private const string MALE_VOICE_ID = "Qrl71rx6Yg8RvyPYRGCQ";
    private const string ELEVEN_LABS_TTS_API_URL = "https://api.elevenlabs.io/v1/text-to-speech/{0}";

    private readonly IConfiguration _configuration;
    private readonly IHttpService _httpService;
    private readonly IFileSharingService _fileSharingService;
    private readonly IClockService _clockService;

    public ElevenLabsAiTextToSpeechProvider(IConfiguration configuration,
        IHttpService httpService,
        IFileSharingService fileSharingService,
        IClockService clockService)
    {
        _configuration = configuration;
        _httpService = httpService;
        _fileSharingService = fileSharingService;
        _clockService = clockService;
    }

    public async Task<string> GetTextToSpeechAudioUrlAsync(string text, VoiceType voiceType,
        CancellationToken cancellationToken = default)
    {
        var key = _configuration.ElevenLabsApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            Log.Error("Missing Eleven Labs API key");
            return null;
        }

        var headers = new Dictionary<string, string>
        {
            ["xi-api-key"] = key
        };

        var dto = new ElevenLabsRequestDto
        {
            Text = text,
            ModelId = "eleven_multilingual_v2"
        };

        var voiceId = voiceType switch
        {
            VoiceType.Male => MALE_VOICE_ID,
            VoiceType.Female => FEMALE_VOICE_ID,
            _ => FEMALE_VOICE_ID
        };

        var stream = await _httpService.SendForStreamAsync(
            HttpRequest.Post(string.Format(ELEVEN_LABS_TTS_API_URL, voiceId)).WithJsonBody(dto).WithHeaders(headers),
            cancellationToken);

        if (stream == null)
        {
            Log.Error("Failed to get TTS audio stream");
            return null;
        }

        var fileName = $"audio/tts_{_clockService.CurrentUtcDateTime:yyyyMMdd_HHmmssfff}.mp3";
        var url = await _fileSharingService.CreateFileAsync(
            stream, fileName, "Text to Speech", "audio/mpeg", cancellationToken);
        if (string.IsNullOrEmpty(url))
        {
            Log.Error("Failed to upload audio file");
            return null;
        }

        return url;
    }
}
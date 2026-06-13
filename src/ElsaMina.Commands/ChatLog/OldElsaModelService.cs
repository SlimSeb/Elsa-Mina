using System.IO.Compression;
using ElsaMina.Logging;
using Lusamine.Markovify;

namespace ElsaMina.Commands.ChatLog;

/// <summary>
/// Loads a precompiled Markov model trained on the old Elsa bot's chat history and generates
/// sentences from it. The model is a large JSON blob shipped zipped under <c>Data/</c>; it is
/// deserialized lazily on first use and then kept in memory for the lifetime of the bot.
/// </summary>
public class OldElsaModelService : IOldElsaModelService
{
    private const string DATA_DIRECTORY_NAME = "Data";
    private const string ZIP_FILE_NAME = "old_elsa_mina_model_pruned.bin.zip";
    private const string BIN_ENTRY_NAME = "old_elsa_mina_model_pruned.bin";

    private const int MAX_WORDS = 40;
    private const int TRIES = 50;

    private readonly Lock _loadLock = new();
    private Text _model;

    public string GenerateSentence()
    {
        EnsureLoaded();
        return _model?.MakeSentence(tries: TRIES, testOutput: false, maxWords: MAX_WORDS);
    }

    public string GenerateSentence(string start)
    {
        if (string.IsNullOrWhiteSpace(start))
        {
            return GenerateSentence();
        }

        EnsureLoaded();
        if (_model == null)
        {
            return null;
        }

        var words = start.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // MakeSentenceWithStart only seeds from up to StateSize words, so when the user provides
        // more, we keep the extra leading words as a literal prefix and seed from the last ones.
        var seedCount = Math.Min(words.Length, _model.StateSize);
        var leadingWords = words[..^seedCount];
        var seed = string.Join(' ', words[^seedCount..]);

        var continuation = _model.MakeSentenceWithStart(seed, strict: false, tries: TRIES, testOutput: false);
        if (string.IsNullOrWhiteSpace(continuation))
        {
            return null;
        }

        return leadingWords.Length == 0
            ? continuation
            : $"{string.Join(' ', leadingWords)} {continuation}";
    }

    private void EnsureLoaded()
    {
        if (_model != null)
        {
            return;
        }

        lock (_loadLock)
        {
            if (_model != null)
            {
                return;
            }

            _model = LoadModel();
        }
    }

    private static Text LoadModel()
    {
        var zipPath = Path.Join(DATA_DIRECTORY_NAME, ZIP_FILE_NAME);

        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var entry = archive.GetEntry(BIN_ENTRY_NAME) ?? archive.Entries.FirstOrDefault();
            if (entry == null)
            {
                Log.Error("Old Elsa Markov model archive {0} contains no entries", zipPath);
                return null;
            }

            using var reader = new BinaryReader(entry.Open());
            return Text.ReadBinary(reader);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to load old Elsa Markov model from {0}", zipPath);
            return null;
        }
    }
}

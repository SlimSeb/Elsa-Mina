using System.Text.Json;
using System.Text.Json.Serialization;

namespace ElsaMina.Commands.Misc.Dictionary;

public class DictionaryApiDefinition
{
    // sseq is a 3-level nested array of ["type", data] tuples - kept as JsonElement
    [JsonPropertyName("sseq")]
    public JsonElement SenseSequence { get; set; }
}

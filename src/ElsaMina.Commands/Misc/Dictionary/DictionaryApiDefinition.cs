using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ElsaMina.Commands.Misc.Dictionary;

public class DictionaryApiDefinition
{
    // sseq is a 3-level nested array of ["type", data] tuples - kept as JToken
    [JsonProperty("sseq")]
    public JToken SenseSequence { get; set; }
}

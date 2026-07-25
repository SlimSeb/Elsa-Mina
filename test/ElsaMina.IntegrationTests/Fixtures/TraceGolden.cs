using System.Text;

namespace ElsaMina.IntegrationTests.Fixtures;

/// <summary>
/// Compares a recorded interaction trace against a golden file checked in next to the tests. A whole
/// deal produces hundreds of interactions, so the expectation lives in a file rather than inline: the
/// failure message then points at the exact line where behaviour drifted.
/// </summary>
public static class TraceGolden
{
    private const string TRACES_FOLDER = "Traces";

    /// <summary>
    /// Asserts the given trace matches <c>Traces/{name}.txt</c>. When the golden file does not exist
    /// yet it is written out and the test fails, so a new trace is never silently accepted.
    /// </summary>
    public static void Verify(string name, IReadOnlyList<string> actual)
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, TRACES_FOLDER, $"{name}.txt");

        if (!File.Exists(path))
        {
            WriteGolden(path, actual);
            Assert.Fail($"Golden trace '{name}' did not exist and has been written to {path}. " +
                        "Review it, copy it next to the test sources, then re-run.");
            return;
        }

        var expected = File.ReadAllLines(path)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (expected.SequenceEqual(actual))
        {
            return;
        }

        Assert.Fail($"Golden trace '{name}' differs:{Environment.NewLine}{Describe(expected, actual)}");
    }

    private static void WriteGolden(string path, IReadOnlyList<string> lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
    }

    /// <summary>
    /// A compact report of the first divergence plus the overall lengths, which is enough to tell a
    /// reordering apart from an extra or missing interaction.
    /// </summary>
    private static string Describe(IReadOnlyList<string> expected, IReadOnlyList<string> actual)
    {
        var report = new StringBuilder();
        report.AppendLine($"expected {expected.Count} entries, got {actual.Count}");

        for (var index = 0; index < Math.Max(expected.Count, actual.Count); index++)
        {
            var expectedEntry = index < expected.Count ? expected[index] : "<none>";
            var actualEntry = index < actual.Count ? actual[index] : "<none>";
            if (expectedEntry == actualEntry)
            {
                continue;
            }

            report.AppendLine($"first difference at line {index + 1}:");
            report.AppendLine($"  expected: {expectedEntry}");
            report.AppendLine($"  actual:   {actualEntry}");
            break;
        }

        return report.ToString();
    }
}

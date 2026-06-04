using ElsaMina.Commands.Games.Semantix;

namespace ElsaMina.UnitTests.Commands.Games.Semantix;

[TestFixture]
public class SemantixMathTest
{
    [Test]
    public void Test_CosineSimilarity_ShouldReturnOne_WhenVectorsAreIdentical()
    {
        float[] vector = [0.5f, 0.3f, -0.2f];

        var similarity = SemantixMath.CosineSimilarity(vector, vector);

        Assert.That(similarity, Is.EqualTo(1).Within(0.0001));
    }

    [Test]
    public void Test_CosineSimilarity_ShouldReturnZero_WhenVectorsAreOrthogonal()
    {
        float[] vectorA = [1f, 0f];
        float[] vectorB = [0f, 1f];

        var similarity = SemantixMath.CosineSimilarity(vectorA, vectorB);

        Assert.That(similarity, Is.EqualTo(0).Within(0.0001));
    }

    [Test]
    public void Test_CosineSimilarity_ShouldReturnMinusOne_WhenVectorsAreOpposite()
    {
        float[] vectorA = [1f, 2f];
        float[] vectorB = [-1f, -2f];

        var similarity = SemantixMath.CosineSimilarity(vectorA, vectorB);

        Assert.That(similarity, Is.EqualTo(-1).Within(0.0001));
    }

    [Test]
    public void Test_CosineSimilarity_ShouldReturnZero_WhenVectorsHaveDifferentLengths()
    {
        float[] vectorA = [1f, 2f];
        float[] vectorB = [1f, 2f, 3f];

        var similarity = SemantixMath.CosineSimilarity(vectorA, vectorB);

        Assert.That(similarity, Is.EqualTo(0));
    }

    [Test]
    public void Test_CosineSimilarity_ShouldReturnZero_WhenVectorIsNull()
    {
        Assert.That(SemantixMath.CosineSimilarity(null, [1f]), Is.EqualTo(0));
    }

    [Test]
    public void Test_ToTemperature_ShouldClampToMinimum_WhenSimilarityIsVeryLow()
    {
        var temperature = SemantixMath.ToTemperature(-1);

        Assert.That(temperature, Is.EqualTo(SemantixConstants.MIN_TEMPERATURE));
    }

    [Test]
    public void Test_ToTemperature_ShouldClampToMaximum_WhenSimilarityIsVeryHigh()
    {
        var temperature = SemantixMath.ToTemperature(1);

        Assert.That(temperature, Is.EqualTo(SemantixConstants.MAX_TEMPERATURE));
    }

    [Test]
    public void Test_ToTemperature_ShouldIncrease_WhenSimilarityIncreases()
    {
        var cold = SemantixMath.ToTemperature(0.70);
        var warm = SemantixMath.ToTemperature(0.78);
        var hot = SemantixMath.ToTemperature(0.84);

        Assert.That(cold, Is.LessThan(warm));
        Assert.That(warm, Is.LessThan(hot));
    }

    [Test]
    public void Test_ToTemperature_ShouldKeepLooselyRelatedWordsCold()
    {
        // LLM embeddings put unrelated words around 0.55-0.65 cosine; the gamma curve
        // must keep that cluster cold rather than bunched near 70°.
        var looselyRelated = SemantixMath.ToTemperature(0.62);

        Assert.That(looselyRelated, Is.LessThan(30));
    }

    [Test]
    public void Test_ToTemperature_ShouldHeatUpOnlyVeryCloseWords()
    {
        var veryClose = SemantixMath.ToTemperature(0.83);

        Assert.That(veryClose, Is.GreaterThan(75));
    }

    [Test]
    public void Test_SerializeVector_ShouldRoundTrip()
    {
        float[] vector = [0.1f, -0.5f, 3.14f, 0f];

        var roundTripped = SemantixMath.DeserializeVector(SemantixMath.SerializeVector(vector));

        Assert.That(roundTripped, Is.EqualTo(vector));
    }
}

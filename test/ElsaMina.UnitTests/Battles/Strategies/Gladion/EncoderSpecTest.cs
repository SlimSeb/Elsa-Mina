using System.Text.Json.Nodes;
using ElsaMina.Battles.Strategies.Gladion;

namespace ElsaMina.UnitTests.Battles.Strategies.Gladion;

/// <summary>
/// The checks every model version's spec goes through, on the released GladionV1_22M spec (V2.1's observation) and
/// on a V1 spec. Feature sizes are checked by each version's encoder.
/// </summary>
public class EncoderSpecTest
{
    private static readonly string SPEC_PATH =
        Path.Join(AppContext.BaseDirectory, "Models", "GladionV1_22M", "encoder_spec.json");
    private static readonly string[] V2_1_INPUTS =
    [
        "cand_ids", "cand_feats", "prior_ids", "prior_w", "prior_feats", "hist_ids", "hist_feats", "effect_feats",
        "cand_effect_feats"
    ];
    private static readonly int[] CANDIDATE_FEATURES_SHAPE = [6, 8, 40];

    private string _tamperedSpecPath;

    [SetUp]
    public void SetUp()
    {
        _tamperedSpecPath = Path.Join(Path.GetTempPath(), $"encoder_spec_{Guid.NewGuid():N}.json");
    }

    [TearDown]
    public void TearDown()
    {
        File.Delete(_tamperedSpecPath);
    }

    private string WriteTamperedSpec(Action<JsonNode> tamper)
    {
        var spec = JsonNode.Parse(File.ReadAllText(SPEC_PATH));
        tamper(spec);
        File.WriteAllText(_tamperedSpecPath, spec.ToJsonString());
        return _tamperedSpecPath;
    }

    [Test]
    public void Test_Load_ShouldReadExportedSpec_WhenItIsValid()
    {
        // Act
        var spec = EncoderSpec.Load(SPEC_PATH);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(spec.MonFeatureSize, Is.EqualTo(84));
            Assert.That(spec.MoveFeatureSize, Is.EqualTo(39));
            Assert.That(spec.FieldFeatureSize, Is.EqualTo(81));
            Assert.That(spec.Vocabularies.Species["garchomp"], Is.GreaterThanOrEqualTo(2));
            Assert.That(spec.Orderings.Effects, Has.Count.EqualTo(10));
        }
    }

    [Test]
    public void Test_Load_ShouldReadNoExtraInputs_WhenSpecIsV1()
    {
        // Arrange
        File.WriteAllText(_tamperedSpecPath, GladionEncoderV1Spec.Create().ToJsonString());

        // Act
        var spec = EncoderSpec.Load(_tamperedSpecPath);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(spec.EncoderVersion, Is.EqualTo(1));
            Assert.That(spec.ExtraInputs, Is.Empty);
            Assert.That(spec.Model, Is.Null);
            Assert.That(spec.SetPriors, Is.Null);
        }
    }

    [Test]
    public void Test_Load_ShouldReadInputsAndSetPriors_WhenSpecIsEncoderV2()
    {
        // Act
        var spec = EncoderSpec.Load(SPEC_PATH);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            Assert.That(spec.EncoderVersion, Is.EqualTo(2));
            Assert.That(spec.ExtraInputs.Select(input => input.Name), Is.EqualTo(V2_1_INPUTS));
            Assert.That(spec.Inputs.Single(input => input.Name == "cand_feats").GetDimensions(),
                Is.EqualTo(CANDIDATE_FEATURES_SHAPE));
            Assert.That(spec.Model.CandidateMoves, Is.EqualTo(8));
            Assert.That(spec.Model.History, Is.True);
            Assert.That(spec.Model.MoveEffects, Is.True);
            Assert.That(spec.SetPriors.File, Is.EqualTo("set_priors.json"));
            Assert.That(spec.SetPriors.AbilitiesAndItems, Is.EqualTo(4));
        }
    }

    [Test]
    public void Test_Load_ShouldThrow_WhenInputsDontStartWithTheSharedOnes()
    {
        // Arrange
        var path = WriteTamperedSpec(spec => spec["inputs"][0]["name"] = "species_ids");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => EncoderSpec.Load(path));
        Assert.That(exception.Message, Does.Contain("inputs"));
    }

    [Test]
    public void Test_Load_ShouldThrow_WhenOrderingHasNameTheTrackerNeverProduces()
    {
        // Arrange: an extra weather would also change f_field, so swap one name instead
        var path = WriteTamperedSpec(spec => spec["orderings"]["weathers"][1] = "ACID_RAIN");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => EncoderSpec.Load(path));
        Assert.That(exception.Message, Does.Contain("weathers"));
    }

    [Test]
    public void Test_Load_ShouldThrow_WhenItemHashingChanges()
    {
        // Arrange
        var path = WriteTamperedSpec(spec => spec["item_hash"]["algorithm"] = "md5(item_id) % buckets + 2");

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => EncoderSpec.Load(path));
        Assert.That(exception.Message, Does.Contain("item hash"));
    }

    [Test]
    public void Test_Load_ShouldThrow_WhenActionCountDiffers()
    {
        // Arrange
        var path = WriteTamperedSpec(spec => spec["act_dim"] = 26);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => EncoderSpec.Load(path));
        Assert.That(exception.Message, Does.Contain("act_dim"));
    }
}

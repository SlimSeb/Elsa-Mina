using ElsaMina.Battles.Strategies.MiniPoke;

namespace ElsaMina.UnitTests.Battles.Strategies.MiniPoke;

public class MiniPokeValueCalibrationTest
{
    private string _directory;

    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("minipoke").FullName;
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_directory, true);
    }

    [Test]
    public void Test_Load_ShouldReadScaleAndOffset_WhenFileExists()
    {
        // Arrange
        var path = Path.Join(_directory, "value_calibration.json");
        File.WriteAllText(path, """{"model": "MiniPokeV2_1M", "scale": 0.8, "offset": -0.2, "decisions": 10}""");

        // Act
        var calibration = MiniPokeValueCalibration.Load(path);

        // Assert
        Assert.That(calibration.Apply(0.5), Is.EqualTo(0.2).Within(1e-12));
    }

    [Test]
    public void Test_Load_ShouldReturnIdentity_WhenFileIsMissing()
    {
        // Act
        var calibration = MiniPokeValueCalibration.Load(Path.Join(_directory, "missing.json"));

        // Assert
        Assert.That(calibration.Apply(0.37), Is.EqualTo(0.37));
    }

    [Test]
    public void Test_Load_ShouldReturnIdentity_WhenFileIsNotJson()
    {
        // Arrange
        var path = Path.Join(_directory, "value_calibration.json");
        File.WriteAllText(path, "not json");

        // Act
        var calibration = MiniPokeValueCalibration.Load(path);

        // Assert
        Assert.That(calibration, Is.SameAs(MiniPokeValueCalibration.Identity));
    }
}

using ElsaMina.Battles.Strategies.Gladion;

namespace ElsaMina.UnitTests.Battles.Strategies.Gladion;

public class GladionValueCalibrationTest
{
    private string _directory;

    [SetUp]
    public void SetUp()
    {
        _directory = Directory.CreateTempSubdirectory("gladion").FullName;
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
        File.WriteAllText(path, """{"model": "GladionV1_22M", "scale": 0.8, "offset": -0.2, "decisions": 10}""");

        // Act
        var calibration = GladionValueCalibration.Load(path);

        // Assert
        Assert.That(calibration.Apply(0.5), Is.EqualTo(0.2).Within(1e-12));
    }

    [Test]
    public void Test_Load_ShouldReturnIdentity_WhenFileIsMissing()
    {
        // Act
        var calibration = GladionValueCalibration.Load(Path.Join(_directory, "missing.json"));

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
        var calibration = GladionValueCalibration.Load(path);

        // Assert
        Assert.That(calibration, Is.SameAs(GladionValueCalibration.Identity));
    }
}

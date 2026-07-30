using ElsaMina.Commands.Dolls;

namespace ElsaMina.UnitTests.Commands.Dolls;

public class DollCatalogueNamingTest
{
    [TestCase("Petites 16x16", 16)]
    [TestCase("Moyennes 24x24", 24)]
    [TestCase("Grandes 32x32", 32)]
    [TestCase("Statues 48 x 48", 48)]
    [TestCase("Trophées 24×24", 24)]
    public void Test_TryParseSize_ShouldReadTheSizeFromTheFolderName(string folderName, int expectedSize)
    {
        // Act
        var result = DollCatalogueNaming.TryParseSize(folderName, out var size);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(size, Is.EqualTo(expectedSize));
    }

    [TestCase("Brouillons")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("Enormes 512x512")]
    [TestCase("Minuscules 2x2")]
    public void Test_TryParseSize_ShouldFail_WhenNoUsableSizeIsPresent(string folderName)
    {
        // Act & Assert
        Assert.That(DollCatalogueNaming.TryParseSize(folderName, out _), Is.False);
    }

    [TestCase("pikachu.png", "pikachu")]
    [TestCase("pikachu_16x16.png", "pikachu")]
    [TestCase("riolu_debout_face_deux_pieds_32x32.png", "rioludeboutfacedeuxpieds")]
    [TestCase("Mr. Mime 24x24.png", "mrmime")]
    [TestCase("Doll_Big_Snorlax_II.png", "bigsnorlaxii")]
    [TestCase("Doll_Surf_Pikachu_II.png", "surfpikachuii")]
    public void Test_ToDollId_ShouldSlugifyTheFileNameWithoutItsPrefixOrSizeSuffix(string fileName, string expectedId)
    {
        // Act & Assert
        Assert.That(DollCatalogueNaming.ToDollId(fileName), Is.EqualTo(expectedId));
    }

    [Test]
    public void Test_ToDollId_ShouldKeepTheGenerationApart_SoVariantsDoNotCollide()
    {
        // Act & Assert
        Assert.That(DollCatalogueNaming.ToDollId("Doll_Bulbasaur_II.png"),
            Is.Not.EqualTo(DollCatalogueNaming.ToDollId("Doll_Bulbasaur_IV.png")));
    }

    [TestCase("pikachu.png", "Pikachu")]
    [TestCase("pikachu_16x16.png", "Pikachu")]
    [TestCase("riolu_debout_face_deux_pieds_32x32.png", "Riolu debout face deux pieds")]
    [TestCase("melofee-assise_24x24.png", "Melofee assise")]
    [TestCase("Doll_Big_Snorlax_II.png", "Big Snorlax II")]
    [TestCase("Doll_Wailord_IV.png", "Wailord IV")]
    public void Test_ToDisplayName_ShouldMakeTheFileNameReadable(string fileName, string expectedName)
    {
        // Act & Assert
        Assert.That(DollCatalogueNaming.ToDisplayName(fileName), Is.EqualTo(expectedName));
    }
}

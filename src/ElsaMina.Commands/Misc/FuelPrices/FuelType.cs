namespace ElsaMina.Commands.Misc.FuelPrices;

/// <summary>
/// A fuel sold in French stations. <see cref="FieldName"/> is the prefix of its columns in the dataset
/// (<c>gazole_prix</c>, <c>gazole_maj</c>).
/// </summary>
public sealed record FuelType(string FieldName)
{
    public static IReadOnlyList<FuelType> All { get; } =
    [
        new("gazole"),
        new("e10"),
        new("sp98"),
        new("sp95"),
        new("e85"),
        new("gplc")
    ];

    public string PriceField => $"{FieldName}_prix";
    public string UpdateDateField => $"{FieldName}_maj";
    public string NameResourceKey => $"fuelprices_fuel_{FieldName}";
}

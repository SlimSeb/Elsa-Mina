namespace ElsaMina.DataAccess.Models;

public class VoltorbFlipLevel
{
    public int Level { get; set; }
    public int MaxLevel { get; set; }
    public int Coins { get; set; }
    public string UserId { get; set; }
    public SavedUser User { get; set; }
}
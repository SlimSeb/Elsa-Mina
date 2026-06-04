namespace ElsaMina.Commands.Games.Semantix;

public static class SemantixMath
{
    public static double CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA == null || vectorB == null || vectorA.Length != vectorB.Length || vectorA.Length == 0)
        {
            return 0;
        }

        double dotProduct = 0, normA = 0, normB = 0;
        for (var i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        if (normA == 0 || normB == 0)
        {
            return 0;
        }

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    public static int ToTemperature(double similarity)
    {
        var span = SemantixConstants.SIMILARITY_CEILING - SemantixConstants.SIMILARITY_FLOOR;
        var normalized = Math.Clamp((similarity - SemantixConstants.SIMILARITY_FLOOR) / span, 0, 1);
        var curved = Math.Pow(normalized, SemantixConstants.TEMPERATURE_GAMMA);
        var temperature = curved * (SemantixConstants.MAX_TEMPERATURE - SemantixConstants.MIN_TEMPERATURE)
                          + SemantixConstants.MIN_TEMPERATURE;
        return (int)Math.Round(temperature);
    }

    public static byte[] SerializeVector(float[] vector)
    {
        var bytes = new byte[vector.Length * sizeof(float)];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public static float[] DeserializeVector(byte[] bytes)
    {
        var vector = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }
}

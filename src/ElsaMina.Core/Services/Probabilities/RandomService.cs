using System.Security.Cryptography;

namespace ElsaMina.Core.Services.Probabilities;

public class RandomService : IRandomService
{
    private readonly Random _rng = new();

    public T RandomElement<T>(IEnumerable<T> enumerable)
    {
        var list = enumerable.ToList();
        return list[_rng.Next(0, list.Count)];
    }

    public IEnumerable<T> RandomSample<T>(IEnumerable<T> enumerable, int count)
    {
        var list = enumerable.ToList();
        if (count >= list.Count)
        {
            return list;
        }

        var selectedIndices = new HashSet<int>();
        while (selectedIndices.Count < count)
        {
            selectedIndices.Add(_rng.Next(0, list.Count));
        }

        return selectedIndices.Select(i => list[i]);
    }

    public void ShuffleInPlace<T>(IList<T> list)
    {
        var n = list.Count;
        while (n > 1)
        {
            n--;
            var k = _rng.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }

    public double NextDouble()
    {
        return _rng.NextDouble();
    }

    public int NextInt(int lowerBound, int upperBound)
    {
        return _rng.Next(lowerBound, upperBound);
    }
    
    public int NextInt(int upperBound)
    {
        return _rng.Next(upperBound);
    }
}
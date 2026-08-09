namespace ElsaMina.Commands.Misc.RandomImages;

/// <summary>
/// A single search hit, carrying the small variant used for the mosaic thumbnail and the larger
/// variant that gets posted in chat once the user picks it.
/// </summary>
public record GifSearchResult(GifMediaInfo Preview, GifMediaInfo Full);

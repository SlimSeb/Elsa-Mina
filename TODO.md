# TODO

* Dockeriser pour faciliter le deploy
* Rework des options
* Hotreload des commandes (possible ?)
* Chemin glacé / Wordle

## Ideas cooked by Claude
A few ideas, roughly ordered by usefulness:

1. Peak ELO annotation — mark the highest ELO point on the graph with a dot and
   label. Easy to implement and immediately readable.
2. Moving average — overlay a smoothed line (e.g. 7-day or 24-point rolling average)
   on the scatter. More honest than linear regression for volatile ELO curves, and
   complements the trend line well.
3. ELO distribution histogram — a separate command showing the distribution of
   recorded ELO values as a histogram, revealing whether a player clusters around a
   rating or swings wildly.
4. Multi-user comparison — accept multiple usernames and overlay their ELO curves on
   the same graph with different colors. Useful for head-to-head comparisons within a
   room.
5. Session detection — group snapshots into "sessions" (gaps > N hours) and shade the
   background by session. Helps visually separate grinding sessions from inactivity
   periods.
6. Volatility metric — show standard deviation of ELO alongside slope/R², giving a
   single number for how much the player oscillates. Pairs naturally with the trend
   annotation you already have.

Of these, peak annotation is the lowest effort / highest payoff, and moving average
would make the existing trend commands meaningfully more informative since it doesn't
assume linearity.

✻ Worked for 11s
- ELO percentile over time — if you snapshot multiple users in the same format, you
  can rank them against each other at each point in time
- Format comparison graph — overlay ELO trends for the same user across multiple
  formats on one chart

Requiring new data collection:
- Win rate tracker — scrape the Showdown API's user stats endpoint and store win/loss
  counts over time; graph win rate trend separately from ELO
- Activity heatmap — games played per day of week / hour of day (needs game
  timestamps, not just ELO snapshots)
- Format popularity trend — track how many active ladder players a format has over
  time; useful for detecting dying/growing metas
- Ladder leaderboard snapshots — periodically store top-N players per format and
  graph how the leaderboard shifts

More ambitious:
- ELO momentum indicator — weighted recent ELO delta vs. long-term average, like a
  MACD for ladder performance
- Regression to mean detection — flag when a player's recent ELO is a statistically
  unlikely outlier vs. their historical distribution (hot/cold streak alert)

The win rate tracker + activity heatmap pair would probably give the most value to
users since ELO alone doesn't tell the full story.

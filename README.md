# Chess Engine 2DArray C#

A chess engine written in C# (.NET 9), with a UCI interface for chess GUIs and a Windows desktop app to play against it.

## Strength

In a 40-game match at 60s + 0.6s per move against Stockfish 19 limited to `UCI_Elo 1500`, the engine scored **+39 =0 -1**. That puts it roughly 600 Elo above that level, on Stockfish's engine-rating (CCRL) scale.

## Features

**Engine** (`Chess Engine 2DArray C#/`)
- 64-square board (rank 0 = 8th rank) with make/unmake and Zobrist hashing
- Fully legal move generation, verified against the standard perft positions
- Iterative-deepening principal variation search with aspiration windows
- Transposition table, quiescence search, check extensions
- Null-move pruning, late move reductions, reverse futility / futility / late move pruning
- Move ordering: hash move, MVV-LVA captures, killer moves, history heuristic
- Tapered evaluation using the PeSTO piece-square tables, plus pawn structure (doubled, isolated, passed), bishop pair, rooks on open files and endgame mop-up
- Repetition, 50-move and insufficient-material draw detection
- Time management for `wtime/btime/winc/binc/movestogo/movetime`

**Desktop app** (`ChessGui/`)
- Play as White or Black, with click-to-move and legal-move hints
- Lichess-style pieces, sliding move animations and move/capture sounds
- Take back, flip board, copy FEN, adjustable engine think time
- Live engine evaluation and a move list in algebraic notation

## Build and run

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build -c Release
```

Play in the desktop app (Windows):

```bash
dotnet run -c Release --project ChessGui
```

Run the UCI engine. Point a GUI such as Arena, Cute Chess or BanksiaGUI at `ChessEngine.exe` in `Chess Engine 2DArray C#/bin/Release/net9.0/`, or run it directly:

```bash
dotnet run -c Release --project "Chess Engine 2DArray C#"
```

Besides the standard UCI commands, the engine accepts these extra commands:

| Command | What it does |
| --- | --- |
| `d` | print the board and FEN |
| `eval` | static evaluation of the current position |
| `perft <depth>` | count leaf nodes, split by move |
| `bench [depth]` | fixed-depth search over several positions (speed check) |
| `play [white\|black] [ms]` | play a game in the terminal |
| `status` | legal-move count, check and draw state (for match scripts) |

To verify move generation against the published perft results:

```bash
dotnet run -c Release --project "Chess Engine 2DArray C#" -- perftsuite
```

## Credits

- Piece-square tables: PeSTO by Ronald Friederich.
- Piece images ("cburnett", GPLv2+) and sounds (AGPLv3+) come from [lichess.org](https://github.com/lichess-org/lila). See [ChessGui/Assets/ATTRIBUTION.md](ChessGui/Assets/ATTRIBUTION.md).
- `Chess Engine 2DArray C#/_original/` holds the earlier version of this project, before the rewrite. It is kept for reference and is not compiled.

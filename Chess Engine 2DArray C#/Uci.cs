using System.Diagnostics;

namespace Chess_Engine_2DArray_C_
{
    // Universal Chess Interface front-end, so the engine can be loaded into Arena, Cute Chess,
    // BanksiaGUI, lichess-bot, etc. Also accepts a few debugging commands: d, eval, perft, bench, play.
    public sealed class Uci
    {
        private const string EngineName = "Chess Engine 2DArray C#";
        private const int DefaultHashMb = 64;
        private const int MoveOverheadMs = 30;

        private readonly Board board = new Board();
        private readonly TranspositionTable tt = new TranspositionTable(DefaultHashMb);
        private readonly Search search;
        private Task? searchTask;

        public Uci()
        {
            search = new Search(board, tt) { Info = Console.WriteLine };
        }

        public void Run()
        {
            string? line;
            while ((line = Console.ReadLine()) != null)
            {
                try
                {
                    if (!Handle(line.Trim())) break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"info string error: {ex.Message}");
                }
            }
            StopSearch();
        }

        public void Execute(string command)
        {
            Handle(command.Trim());
            WaitForSearch();
        }

        private bool Handle(string line)
        {
            string[] tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return true;

            switch (tokens[0])
            {
                case "uci":
                    Console.WriteLine($"id name {EngineName}");
                    Console.WriteLine("id author Adam Seffar");
                    Console.WriteLine($"option name Hash type spin default {DefaultHashMb} min 1 max 1024");
                    Console.WriteLine("uciok");
                    break;
                case "isready":
                    WaitForSearch();
                    Console.WriteLine("readyok");
                    break;
                case "ucinewgame":
                    StopSearch();
                    tt.Clear();
                    search.ClearHistory();
                    break;
                case "setoption":
                    StopSearch();
                    SetOption(tokens);
                    break;
                case "position":
                    StopSearch();
                    SetPosition(tokens);
                    break;
                case "go":
                    StopSearch();
                    Go(tokens);
                    break;
                case "stop":
                    StopSearch();
                    break;
                case "quit":
                    return false;
                case "d":
                    WaitForSearch();
                    board.PrintBoard();
                    Console.WriteLine($"Fen: {board.ToFen()}");
                    Console.WriteLine($"Key: {board.Key:X16}");
                    break;
                case "eval":
                    WaitForSearch();
                    Console.WriteLine($"Static eval (side to move): {Evaluation.Evaluate(board)}");
                    break;
                case "status":
                    // Game-state summary for match scripts acting as an arbiter.
                    WaitForSearch();
                    Console.WriteLine($"status legal {MoveGenerator.GenerateLegalMoves(board).Count} check {(board.InCheck() ? 1 : 0)} " +
                                      $"repetitions {board.RepetitionCount()} halfmove {board.HalfmoveClock} " +
                                      $"insufficient {(board.IsInsufficientMaterial() ? 1 : 0)}");
                    break;
                case "perft":
                    WaitForSearch();
                    Perft(tokens.Length > 1 ? int.Parse(tokens[1]) : 5);
                    break;
                case "bench":
                    WaitForSearch();
                    Bench(tokens.Length > 1 ? int.Parse(tokens[1]) : 8);
                    break;
                case "play":
                    WaitForSearch();
                    Play(tokens);
                    break;
                default:
                    Console.WriteLine($"info string unknown command: {tokens[0]}");
                    break;
            }
            return true;
        }

        private void SetOption(string[] tokens)
        {
            // setoption name Hash value 128
            int nameIndex = Array.IndexOf(tokens, "name");
            int valueIndex = Array.IndexOf(tokens, "value");
            if (nameIndex < 0 || valueIndex < 0 || valueIndex + 1 >= tokens.Length) return;
            string name = string.Join(' ', tokens[(nameIndex + 1)..valueIndex]);
            if (name.Equals("Hash", StringComparison.OrdinalIgnoreCase))
                tt.Resize(int.Parse(tokens[valueIndex + 1]));
        }

        private void SetPosition(string[] tokens)
        {
            // position startpos [moves ...] | position fen <6 fields> [moves ...]
            int movesIndex = Array.IndexOf(tokens, "moves");
            if (tokens.Length > 1 && tokens[1] == "fen")
            {
                int end = movesIndex < 0 ? tokens.Length : movesIndex;
                board.LoadFromFen(string.Join(' ', tokens[2..end]));
            }
            else
            {
                board.LoadFromFen(Board.StartFen);
            }

            if (movesIndex < 0) return;
            for (int i = movesIndex + 1; i < tokens.Length; i++)
            {
                Move move = ParseMove(tokens[i]);
                if (move.IsNone)
                    throw new ArgumentException($"illegal move in position command: {tokens[i]}");
                board.MakeMove(move);
            }
        }

        private Move ParseMove(string text)
        {
            foreach (Move m in MoveGenerator.GenerateLegalMoves(board))
                if (m.ToString() == text.ToLowerInvariant())
                    return m;
            return Move.None;
        }

        private void Go(string[] tokens)
        {
            long wtime = -1, btime = -1, winc = 0, binc = 0, movetime = -1, nodes = -1;
            int movestogo = 0, depth = Search.MaxPly - 1;

            for (int i = 1; i < tokens.Length; i++)
            {
                string next = i + 1 < tokens.Length ? tokens[i + 1] : "0";
                switch (tokens[i])
                {
                    case "wtime": wtime = long.Parse(next); i++; break;
                    case "btime": btime = long.Parse(next); i++; break;
                    case "winc": winc = long.Parse(next); i++; break;
                    case "binc": binc = long.Parse(next); i++; break;
                    case "movestogo": movestogo = int.Parse(next); i++; break;
                    case "movetime": movetime = long.Parse(next); i++; break;
                    case "depth": depth = Math.Clamp(int.Parse(next), 1, Search.MaxPly - 1); i++; break;
                    case "nodes": nodes = long.Parse(next); i++; break;
                }
            }

            var limits = new SearchLimits { MaxDepth = depth, MaxNodes = nodes };
            long time = board.WhiteToMove ? wtime : btime;
            long inc = board.WhiteToMove ? winc : binc;

            if (movetime > 0)
            {
                limits.HardTimeMs = limits.SoftTimeMs = Math.Max(1, movetime - MoveOverheadMs);
            }
            else if (time >= 0)
            {
                // Spend roughly 1/30 of the remaining time plus most of the increment, and never
                // more than a fraction of the clock even when an iteration runs long.
                int movesLeft = movestogo > 0 ? Math.Min(movestogo + 1, 40) : 30;
                long allocation = time / movesLeft + inc * 3 / 4;
                long maximum = Math.Max(1, Math.Min(time / 3, time - MoveOverheadMs));
                limits.SoftTimeMs = Math.Max(1, Math.Min(allocation * 6 / 10, maximum));
                limits.HardTimeMs = Math.Max(1, Math.Min(allocation * 5 / 2, maximum));
            }

            searchTask = Task.Run(() =>
            {
                Move best = search.Think(limits, out _);
                Console.WriteLine($"bestmove {best}");
            });
        }

        private void StopSearch()
        {
            if (searchTask == null) return;
            search.Stop();
            searchTask.Wait();
            searchTask = null;
        }

        private void WaitForSearch()
        {
            searchTask?.Wait();
            searchTask = null;
        }

        private void Perft(int depth)
        {
            var sw = Stopwatch.StartNew();
            long total = 0;
            foreach (Move m in MoveGenerator.GenerateLegalMoves(board))
            {
                board.MakeMove(m);
                long n = MoveGenerator.Perft(board, depth - 1);
                board.UnmakeMove();
                Console.WriteLine($"{m}: {n}");
                total += n;
            }
            long ms = Math.Max(1, sw.ElapsedMilliseconds);
            Console.WriteLine($"\nNodes searched: {total} ({ms} ms, {total * 1000 / ms} nps)");
        }

        // Fixed-depth search over a few positions; useful to compare speed between versions.
        private void Bench(int depth)
        {
            string[] fens =
            {
                Board.StartFen,
                "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1",
                "r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10",
                "8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1",
                "rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8",
            };

            var benchBoard = new Board();
            var benchSearch = new Search(benchBoard, new TranspositionTable(16));
            long totalNodes = 0;
            var sw = Stopwatch.StartNew();
            foreach (string fen in fens)
            {
                benchBoard.LoadFromFen(fen);
                Move best = benchSearch.Think(new SearchLimits { MaxDepth = depth }, out int score);
                Console.WriteLine($"{fen}\n  bestmove {best} score {Search.FormatScore(score)} nodes {benchSearch.Nodes}");
                totalNodes += benchSearch.Nodes;
            }
            long ms = Math.Max(1, sw.ElapsedMilliseconds);
            Console.WriteLine($"\nBench: {totalNodes} nodes, {ms} ms, {totalNodes * 1000 / ms} nps");
        }

        // play [white|black] [ms per move]: a quick game against the engine in the terminal.
        private void Play(string[] tokens)
        {
            bool humanWhite = tokens.Length < 2 || tokens[1] != "black";
            long moveTime = tokens.Length > 2 ? long.Parse(tokens[2]) : 2000;
            board.LoadFromFen(Board.StartFen);
            tt.Clear();
            search.ClearHistory();
            var quietSearch = new Search(board, tt);

            Console.WriteLine("Enter moves like e2e4 or e7e8q. Type 'quit' to stop.");
            while (true)
            {
                board.PrintBoard();
                List<Move> legal = MoveGenerator.GenerateLegalMoves(board);
                if (legal.Count == 0)
                {
                    Console.WriteLine(board.InCheck() ? "Checkmate!" : "Stalemate.");
                    return;
                }
                if (board.HalfmoveClock >= 100 || board.RepetitionCount() >= 3)
                {
                    Console.WriteLine("Draw.");
                    return;
                }

                if (board.WhiteToMove == humanWhite)
                {
                    Console.Write("Your move: ");
                    string? input = Console.ReadLine()?.Trim();
                    if (input == null || input == "quit") return;
                    Move move = ParseMove(input);
                    if (move.IsNone)
                    {
                        Console.WriteLine("Illegal move.");
                        continue;
                    }
                    board.MakeMove(move);
                }
                else
                {
                    var limits = new SearchLimits { SoftTimeMs = moveTime, HardTimeMs = moveTime };
                    Move move = quietSearch.Think(limits, out int score);
                    Console.WriteLine($"Engine plays {move} (score {Search.FormatScore(score)}, {quietSearch.Nodes} nodes)");
                    board.MakeMove(move);
                }
            }
        }
    }
}

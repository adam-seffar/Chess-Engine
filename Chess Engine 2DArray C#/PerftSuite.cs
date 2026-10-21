using System.Diagnostics;

namespace Chess_Engine_2DArray_C_
{
    // Checks move generation against published perft node counts
    // (https://www.chessprogramming.org/Perft_Results). Any mismatch means a movegen bug.
    public static class PerftSuite
    {
        private static readonly (string Fen, long[] Expected)[] Positions =
        {
            (Board.StartFen, new long[] { 20, 400, 8902, 197281, 4865609 }),
            ("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1", new long[] { 48, 2039, 97862, 4085603 }),
            ("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1", new long[] { 14, 191, 2812, 43238, 674624 }),
            ("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1", new long[] { 6, 264, 9467, 422333 }),
            ("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8", new long[] { 44, 1486, 62379, 2103487 }),
            ("r4rk1/1pp1qppp/p1np1n2/2b1p1B1/2B1P1b1/P1NP1N2/1PP1QPPP/R4RK1 w - - 0 10", new long[] { 46, 2079, 89890, 3894594 }),
        };

        public static bool Run()
        {
            bool allPassed = true;
            var board = new Board();
            var sw = Stopwatch.StartNew();

            foreach (var (fen, expected) in Positions)
            {
                board.LoadFromFen(fen);
                ulong keyBefore = board.Key;
                for (int depth = 1; depth <= expected.Length; depth++)
                {
                    long nodes = MoveGenerator.Perft(board, depth);
                    bool ok = nodes == expected[depth - 1];
                    allPassed &= ok;
                    Console.WriteLine($"{(ok ? "ok  " : "FAIL")} depth {depth}: {nodes} (expected {expected[depth - 1]})  {fen}");
                }
                if (board.Key != keyBefore || board.Key != board.ComputeKey() || board.ToFen() != new Board().Also(b => b.LoadFromFen(fen)).ToFen())
                {
                    allPassed = false;
                    Console.WriteLine($"FAIL board state not restored after unmake: {fen}");
                }
            }

            Console.WriteLine($"\n{(allPassed ? "All perft tests passed" : "PERFT FAILURES")} in {sw.ElapsedMilliseconds} ms");
            return allPassed;
        }

        private static T Also<T>(this T value, Action<T> action)
        {
            action(value);
            return value;
        }
    }
}

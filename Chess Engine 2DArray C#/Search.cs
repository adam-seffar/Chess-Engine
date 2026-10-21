using System.Diagnostics;
using System.Text;

namespace Chess_Engine_2DArray_C_
{
    public sealed class SearchLimits
    {
        public int MaxDepth = Search.MaxPly - 1;
        public long SoftTimeMs = -1;  // don't start a new iteration after this (-1 = no limit)
        public long HardTimeMs = -1;  // abort the search at this point (-1 = no limit)
        public long MaxNodes = -1;
    }

    // Iterative-deepening principal variation search (negamax alpha-beta) with a transposition table,
    // quiescence search, null-move pruning, late move reductions and killer/history move ordering.
    public sealed class Search
    {
        public const int MaxPly = 128;
        public const int Infinity = 32000;
        public const int MateScore = 31000;
        public const int MateBound = MateScore - MaxPly; // scores beyond this are "mate in N"

        private readonly Board board;
        private readonly TranspositionTable tt;

        private readonly Move[][] moveLists = new Move[MaxPly + 1][];
        private readonly int[][] moveScores = new int[MaxPly + 1][];
        private readonly Move[,] killers = new Move[MaxPly + 1, 2];
        private readonly int[,] history = new int[13, 64];
        private readonly Move[,] pvTable = new Move[MaxPly + 1, MaxPly + 1];
        private readonly int[] pvLength = new int[MaxPly + 1];
        private static readonly int[,] LmrTable = BuildLmrTable();

        private SearchLimits limits = new SearchLimits();
        private readonly Stopwatch clock = new Stopwatch();
        private long nodes;
        private volatile bool stopRequested;
        private bool stopped;
        private Move rootBest;

        // Receives UCI "info" lines; null to search silently.
        public Action<string>? Info;

        public long Nodes => nodes;

        public Search(Board board, TranspositionTable tt)
        {
            this.board = board;
            this.tt = tt;
            for (int i = 0; i <= MaxPly; i++)
            {
                moveLists[i] = new Move[MoveGenerator.MaxMoves];
                moveScores[i] = new int[MoveGenerator.MaxMoves];
            }
        }

        private static int[,] BuildLmrTable()
        {
            var table = new int[64, 64];
            for (int d = 1; d < 64; d++)
                for (int m = 1; m < 64; m++)
                    table[d, m] = (int)(0.75 + Math.Log(d) * Math.Log(m) / 2.25);
            return table;
        }

        public void Stop()
        {
            stopRequested = true;
        }

        public void ClearHistory()
        {
            Array.Clear(history);
            Array.Clear(killers);
        }

        public Move Think(SearchLimits searchLimits, out int bestScore)
        {
            limits = searchLimits;
            nodes = 0;
            stopped = false;
            stopRequested = false;
            clock.Restart();
            Array.Clear(killers);
            for (int p = 0; p < 13; p++)
                for (int sq = 0; sq < 64; sq++)
                    history[p, sq] /= 8;

            bestScore = 0;
            List<Move> legal = MoveGenerator.GenerateLegalMoves(board);
            if (legal.Count == 0) return Move.None;

            Move best = legal[0];
            int score = 0;

            for (int depth = 1; depth <= limits.MaxDepth; depth++)
            {
                rootBest = Move.None;
                int result;

                if (depth >= 5)
                {
                    // Aspiration window around the previous score, widened on failure.
                    int delta = 25;
                    int alpha = Math.Max(score - delta, -Infinity);
                    int beta = Math.Min(score + delta, Infinity);
                    while (true)
                    {
                        result = Negamax(depth, 0, alpha, beta, false);
                        if (stopped) break;
                        if (result <= alpha) alpha = Math.Max(result - delta, -Infinity);
                        else if (result >= beta) beta = Math.Min(result + delta, Infinity);
                        else break;
                        delta *= 2;
                        if (delta > 600)
                        {
                            alpha = -Infinity;
                            beta = Infinity;
                        }
                    }
                }
                else
                {
                    result = Negamax(depth, 0, -Infinity, Infinity, false);
                }

                if (stopped)
                {
                    // A move that raised alpha in an unfinished iteration was searched after the
                    // previous best move and beat it, so it is safe to play.
                    if (!rootBest.IsNone) best = rootBest;
                    break;
                }

                if (!rootBest.IsNone) best = rootBest;
                score = result;
                bestScore = score;
                ReportIteration(depth, score);

                if (limits.SoftTimeMs >= 0 && clock.ElapsedMilliseconds >= limits.SoftTimeMs) break;
                if (legal.Count == 1 && limits.SoftTimeMs >= 0) break;
                if (Math.Abs(score) >= MateBound && depth >= 2 * (MateScore - Math.Abs(score)) + 4) break;
            }

            return best;
        }

        private void ReportIteration(int depth, int score)
        {
            if (Info == null) return;
            long ms = Math.Max(1, clock.ElapsedMilliseconds);
            var pv = new StringBuilder();
            for (int i = 0; i < pvLength[0]; i++)
                pv.Append(' ').Append(pvTable[0, i]);
            Info($"info depth {depth} score {FormatScore(score)} nodes {nodes} nps {nodes * 1000 / ms} time {ms} pv{pv}");
        }

        public static string FormatScore(int score)
        {
            if (score >= MateBound) return $"mate {(MateScore - score + 1) / 2}";
            if (score <= -MateBound) return $"mate {-(MateScore + score) / 2}";
            return $"cp {score}";
        }

        private void CheckLimits()
        {
            if (stopRequested ||
                (limits.HardTimeMs >= 0 && clock.ElapsedMilliseconds >= limits.HardTimeMs) ||
                (limits.MaxNodes >= 0 && nodes >= limits.MaxNodes))
                stopped = true;
        }

        private int Negamax(int depth, int ply, int alpha, int beta, bool allowNull)
        {
            pvLength[ply] = ply;
            if ((nodes & 2047) == 0) CheckLimits();
            if (stopped) return 0;

            bool isRoot = ply == 0;
            bool pvNode = beta - alpha > 1;

            if (!isRoot)
            {
                if (board.HalfmoveClock >= 100 || board.IsRepetition()) return 0;

                // Mate distance pruning: no point looking for a longer mate than one already found.
                alpha = Math.Max(alpha, -MateScore + ply);
                beta = Math.Min(beta, MateScore - ply - 1);
                if (alpha >= beta) return alpha;
            }

            bool inCheck = board.InCheck();
            if (inCheck) depth++; // check extension

            if (depth <= 0) return Quiescence(ply, alpha, beta);
            if (ply >= MaxPly - 1) return Evaluation.Evaluate(board);

            nodes++;

            // --- TRANSPOSITION TABLE ---
            Move ttMove = Move.None;
            if (tt.Probe(board.Key, out ttMove, out int ttScore, out int ttDepth, out byte ttFlag))
            {
                ttScore = ScoreFromTT(ttScore, ply);
                if (!pvNode && ttDepth >= depth)
                {
                    if (ttFlag == TranspositionTable.Exact ||
                        (ttFlag == TranspositionTable.LowerBound && ttScore >= beta) ||
                        (ttFlag == TranspositionTable.UpperBound && ttScore <= alpha))
                        return ttScore;
                }
            }

            int staticEval = inCheck ? -Infinity : Evaluation.Evaluate(board);

            if (!pvNode && !inCheck)
            {
                // Reverse futility pruning: far above beta at low depth, assume it holds.
                if (depth <= 6 && Math.Abs(beta) < MateBound && staticEval - 80 * depth >= beta)
                    return staticEval - 80 * depth;

                // Null move pruning: if passing still beats beta, a real move almost surely will.
                if (allowNull && depth >= 3 && staticEval >= beta && Math.Abs(beta) < MateBound &&
                    board.HasNonPawnMaterial(board.WhiteToMove))
                {
                    int reduction = 3 + depth / 6;
                    board.MakeNullMove();
                    int nullScore = -Negamax(depth - 1 - reduction, ply + 1, -beta, -beta + 1, false);
                    board.UnmakeNullMove();
                    if (stopped) return 0;
                    if (nullScore >= beta) return nullScore >= MateBound ? beta : nullScore;
                }
            }

            Move[] moves = moveLists[ply];
            int[] scores = moveScores[ply];
            int count = MoveGenerator.GenerateMoves(board, moves);
            ScoreMoves(moves, scores, count, ttMove, ply);

            int originalAlpha = alpha;
            int bestScore = -Infinity;
            Move bestMove = Move.None;
            int legalMoves = 0;
            int quietMoves = 0;
            bool canFutilityPrune = !pvNode && !inCheck && depth <= 3 && Math.Abs(alpha) < MateBound;
            int futilityValue = staticEval + 100 + 100 * depth;

            for (int i = 0; i < count; i++)
            {
                Move move = PickNext(moves, scores, count, i);
                bool isCapture = board.Squares[move.To] != Piece.Empty || move.Flag == Move.FlagEnPassant;
                bool isQuiet = !isCapture && move.Promotion == Piece.Empty;
                Piece movingPiece = board.Squares[move.From];

                board.MakeMove(move);
                if (board.LastMoveWasIllegal())
                {
                    board.UnmakeMove();
                    continue;
                }
                legalMoves++;
                bool givesCheck = board.InCheck();

                if (isQuiet && !givesCheck && legalMoves > 1)
                {
                    quietMoves++;

                    // Futility pruning: this quiet move can't plausibly lift the score above alpha.
                    bool futile = canFutilityPrune && futilityValue <= alpha;
                    // Late move pruning: at low depth, quiet moves ordered this late rarely matter.
                    bool late = !pvNode && !inCheck && depth <= 4 && quietMoves > 3 + depth * depth * 2;
                    if (futile || late)
                    {
                        board.UnmakeMove();
                        if (futile && futilityValue > bestScore) bestScore = futilityValue;
                        continue;
                    }
                }

                int score;
                if (legalMoves == 1)
                {
                    score = -Negamax(depth - 1, ply + 1, -beta, -alpha, true);
                }
                else
                {
                    int reduction = 0;
                    if (depth >= 3 && legalMoves >= 4 && isQuiet && !inCheck && !givesCheck)
                    {
                        reduction = LmrTable[Math.Min(depth, 63), Math.Min(legalMoves, 63)];
                        if (pvNode) reduction--;
                        if (move == killers[ply, 0] || move == killers[ply, 1]) reduction--;
                        reduction = Math.Clamp(reduction, 0, depth - 2);
                    }

                    // Null-window search first; re-search only if the move looks better than expected.
                    score = -Negamax(depth - 1 - reduction, ply + 1, -alpha - 1, -alpha, true);
                    if (score > alpha && reduction > 0)
                        score = -Negamax(depth - 1, ply + 1, -alpha - 1, -alpha, true);
                    if (score > alpha && score < beta)
                        score = -Negamax(depth - 1, ply + 1, -beta, -alpha, true);
                }

                board.UnmakeMove();
                if (stopped) return 0;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMove = move;

                    if (score > alpha)
                    {
                        alpha = score;
                        UpdatePv(ply, move);
                        if (isRoot) rootBest = move;

                        if (alpha >= beta)
                        {
                            if (isQuiet)
                            {
                                if (killers[ply, 0] != move)
                                {
                                    killers[ply, 1] = killers[ply, 0];
                                    killers[ply, 0] = move;
                                }
                                history[(int)movingPiece, move.To] += depth * depth;
                                if (history[(int)movingPiece, move.To] > 400_000) AgeHistory();
                            }
                            break;
                        }
                    }
                }
            }

            if (legalMoves == 0)
                return inCheck ? -MateScore + ply : 0; // checkmate or stalemate

            byte flag = bestScore >= beta ? TranspositionTable.LowerBound
                      : bestScore > originalAlpha ? TranspositionTable.Exact
                      : TranspositionTable.UpperBound;
            tt.Store(board.Key, bestMove, ScoreToTT(bestScore, ply), depth, flag);

            return bestScore;
        }

        // Search only captures (and queen promotions) until the position is quiet,
        // so the evaluation is never taken in the middle of an exchange.
        private int Quiescence(int ply, int alpha, int beta)
        {
            pvLength[ply] = ply;
            if ((nodes & 2047) == 0) CheckLimits();
            if (stopped) return 0;
            nodes++;

            int standPat = Evaluation.Evaluate(board);
            if (ply >= MaxPly - 1) return standPat;
            if (standPat >= beta) return standPat;
            if (standPat > alpha) alpha = standPat;

            Move[] moves = moveLists[ply];
            int[] scores = moveScores[ply];
            int count = MoveGenerator.GenerateMoves(board, moves, capturesOnly: true);
            ScoreMoves(moves, scores, count, Move.None, ply);

            int bestScore = standPat;
            for (int i = 0; i < count; i++)
            {
                Move move = PickNext(moves, scores, count, i);

                // Delta pruning: even winning this piece outright can't reach alpha.
                Piece victim = move.Flag == Move.FlagEnPassant ? Piece.WP : board.Squares[move.To];
                int gain = victim == Piece.Empty ? 0 : Evaluation.PieceValue[Pieces.Type(victim)];
                if (move.Promotion == Piece.Empty && standPat + gain + 200 <= alpha)
                    continue;

                board.MakeMove(move);
                if (board.LastMoveWasIllegal())
                {
                    board.UnmakeMove();
                    continue;
                }
                int score = -Quiescence(ply + 1, -beta, -alpha);
                board.UnmakeMove();
                if (stopped) return 0;

                if (score > bestScore)
                {
                    bestScore = score;
                    if (score > alpha)
                    {
                        alpha = score;
                        UpdatePv(ply, move);
                        if (alpha >= beta) break;
                    }
                }
            }

            return bestScore;
        }

        // MOVE ORDERING

        private void ScoreMoves(Move[] moves, int[] scores, int count, Move ttMove, int ply)
        {
            Piece[] squares = board.Squares;
            for (int i = 0; i < count; i++)
            {
                Move m = moves[i];
                Piece attacker = squares[m.From];
                Piece victim = m.Flag == Move.FlagEnPassant ? Piece.WP : squares[m.To];

                if (m == ttMove)
                    scores[i] = 2_000_000;
                else if (victim != Piece.Empty)
                    // Most valuable victim, least valuable attacker.
                    scores[i] = 1_000_000 + Evaluation.PieceValue[Pieces.Type(victim)] * 10 - Pieces.Type(attacker);
                else if (m.Promotion != Piece.Empty)
                    scores[i] = Pieces.Type(m.Promotion) == Pieces.Queen ? 950_000 : -1000;
                else if (m == killers[ply, 0])
                    scores[i] = 900_000;
                else if (m == killers[ply, 1])
                    scores[i] = 890_000;
                else
                    scores[i] = history[(int)attacker, m.To];
            }
        }

        // Selection sort one step at a time: cheap when a cutoff comes early.
        private static Move PickNext(Move[] moves, int[] scores, int count, int index)
        {
            int best = index;
            for (int j = index + 1; j < count; j++)
                if (scores[j] > scores[best])
                    best = j;

            if (best != index)
            {
                (moves[index], moves[best]) = (moves[best], moves[index]);
                (scores[index], scores[best]) = (scores[best], scores[index]);
            }
            return moves[index];
        }

        private void AgeHistory()
        {
            for (int p = 0; p < 13; p++)
                for (int sq = 0; sq < 64; sq++)
                    history[p, sq] /= 2;
        }

        private void UpdatePv(int ply, Move move)
        {
            pvTable[ply, ply] = move;
            for (int j = ply + 1; j < pvLength[ply + 1]; j++)
                pvTable[ply, j] = pvTable[ply + 1, j];
            pvLength[ply] = Math.Max(pvLength[ply + 1], ply + 1);
        }

        // Mate scores are stored relative to the node, not the root, so they stay valid
        // when the same position is reached at a different ply.
        private static int ScoreToTT(int score, int ply)
        {
            if (score >= MateBound) return score + ply;
            if (score <= -MateBound) return score - ply;
            return score;
        }

        private static int ScoreFromTT(int score, int ply)
        {
            if (score >= MateBound) return score - ply;
            if (score <= -MateBound) return score + ply;
            return score;
        }
    }
}

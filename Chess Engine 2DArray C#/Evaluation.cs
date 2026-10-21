namespace Chess_Engine_2DArray_C_
{
    // Tapered evaluation: separate middlegame and endgame scores blended by how much material is left.
    // Piece-square tables are the PeSTO tables (Ronald Friederich), laid out a8..h1 for white,
    // which matches this board's square order. Black uses the vertically mirrored square (sq ^ 56).
    // Scores are centipawns from the side to move's point of view.
    public static class Evaluation
    {
        // Simple values used for move ordering and pruning margins.
        public static readonly int[] PieceValue = { 100, 320, 330, 500, 900, 0 };

        private static readonly int[] MgValue = { 82, 337, 365, 477, 1025, 0 };
        private static readonly int[] EgValue = { 94, 281, 297, 512, 936, 0 };
        private static readonly int[] PhaseInc = { 0, 1, 1, 2, 4, 0 };
        private const int MaxPhase = 24;

        private const int Tempo = 10;
        private const int BishopPairMg = 30, BishopPairEg = 50;
        private const int DoubledPawnMg = 10, DoubledPawnEg = 20;
        private const int IsolatedPawnMg = 10, IsolatedPawnEg = 15;
        private const int RookOpenFileMg = 25, RookSemiOpenFileMg = 12;
        // Indexed by how far the pawn has advanced (relative rank 0-7).
        private static readonly int[] PassedPawnMg = { 0, 5, 5, 10, 20, 35, 55, 0 };
        private static readonly int[] PassedPawnEg = { 0, 5, 10, 20, 35, 60, 90, 0 };

        private static readonly int[] MgPawn =
        {
              0,   0,   0,   0,   0,   0,   0,   0,
             98, 134,  61,  95,  68, 126,  34, -11,
             -6,   7,  26,  31,  65,  56,  25, -20,
            -14,  13,   6,  21,  23,  12,  17, -23,
            -27,  -2,  -5,  12,  17,   6,  10, -25,
            -26,  -4,  -4, -10,   3,   3,  33, -12,
            -35,  -1, -20, -23, -15,  24,  38, -22,
              0,   0,   0,   0,   0,   0,   0,   0,
        };
        private static readonly int[] EgPawn =
        {
              0,   0,   0,   0,   0,   0,   0,   0,
            178, 173, 158, 134, 147, 132, 165, 187,
             94, 100,  85,  67,  56,  53,  82,  84,
             32,  24,  13,   5,  -2,   4,  17,  17,
             13,   9,  -3,  -7,  -7,  -8,   3,  -1,
              4,   7,  -6,   1,   0,  -5,  -1,  -8,
             13,   8,   8,  10,  13,   0,   2,  -7,
              0,   0,   0,   0,   0,   0,   0,   0,
        };
        private static readonly int[] MgKnight =
        {
            -167, -89, -34, -49,  61, -97, -15, -107,
             -73, -41,  72,  36,  23,  62,   7,  -17,
             -47,  60,  37,  65,  84, 129,  73,   44,
              -9,  17,  19,  53,  37,  69,  18,   22,
             -13,   4,  16,  13,  28,  19,  21,   -8,
             -23,  -9,  12,  10,  19,  17,  25,  -16,
             -29, -53, -12,  -3,  -1,  18, -14,  -19,
            -105, -21, -58, -33, -17, -28, -19,  -23,
        };
        private static readonly int[] EgKnight =
        {
            -58, -38, -13, -28, -31, -27, -63, -99,
            -25,  -8, -25,  -2,  -9, -25, -24, -52,
            -24, -20,  10,   9,  -1,  -9, -19, -41,
            -17,   3,  22,  22,  22,  11,   8, -18,
            -18,  -6,  16,  25,  16,  17,   4, -18,
            -23,  -3,  -1,  15,  10,  -3, -20, -22,
            -42, -20, -10,  -5,  -2, -20, -23, -44,
            -29, -51, -23, -15, -22, -18, -50, -64,
        };
        private static readonly int[] MgBishop =
        {
            -29,   4, -82, -37, -25, -42,   7,  -8,
            -26,  16, -18, -13,  30,  59,  18, -47,
            -16,  37,  43,  40,  35,  50,  37,  -2,
             -4,   5,  19,  50,  37,  37,   7,  -2,
             -6,  13,  13,  26,  34,  12,  10,   4,
              0,  15,  15,  15,  14,  27,  18,  10,
              4,  15,  16,   0,   7,  21,  33,   1,
            -33,  -3, -14, -21, -13, -12, -39, -21,
        };
        private static readonly int[] EgBishop =
        {
            -14, -21, -11,  -8,  -7,  -9, -17, -24,
             -8,  -4,   7, -12,  -3, -13,  -4, -14,
              2,  -8,   0,  -1,  -2,   6,   0,   4,
             -3,   9,  12,   9,  14,  10,   3,   2,
             -6,   3,  13,  19,   7,  10,  -3,  -9,
            -12,  -3,   8,  10,  13,   3,  -7, -15,
            -14, -18,  -7,  -1,   4,  -9, -15, -27,
            -23,  -9, -23,  -5,  -9, -16,  -5, -17,
        };
        private static readonly int[] MgRook =
        {
             32,  42,  32,  51,  63,   9,  31,  43,
             27,  32,  58,  62,  80,  67,  26,  44,
             -5,  19,  26,  36,  17,  45,  61,  16,
            -24, -11,   7,  26,  24,  35,  -8, -20,
            -36, -26, -12,  -1,   9,  -7,   6, -23,
            -45, -25, -16, -17,   3,   0,  -5, -33,
            -44, -16, -20,  -9,  -1,  11,  -6, -71,
            -19, -13,   1,  17,  16,   7, -37, -26,
        };
        private static readonly int[] EgRook =
        {
             13,  10,  18,  15,  12,  12,   8,   5,
             11,  13,  13,  11,  -3,   3,   8,   3,
              7,   7,   7,   5,   4,  -3,  -5,  -3,
              4,   3,  13,   1,   2,   1,  -1,   2,
              3,   5,   8,   4,  -5,  -6,  -8, -11,
             -4,   0,  -5,  -1,  -7, -12,  -8, -16,
             -6,  -6,   0,   2,  -9,  -9, -11,  -3,
             -9,   2,   3,  -1,  -5, -13,   4, -20,
        };
        private static readonly int[] MgQueen =
        {
            -28,   0,  29,  12,  59,  44,  43,  45,
            -24, -39,  -5,   1, -16,  57,  28,  54,
            -13, -17,   7,   8,  29,  56,  47,  57,
            -27, -27, -16, -16,  -1,  17,  -2,   1,
             -9, -26,  -9, -10,  -2,  -4,   3,  -3,
            -14,   2, -11,  -2,  -5,   2,  14,   5,
            -35,  -8,  11,   2,   8,  15,  -3,   1,
             -1, -18,  -9,  10, -15, -25, -31, -50,
        };
        private static readonly int[] EgQueen =
        {
             -9,  22,  22,  27,  27,  19,  10,  20,
            -17,  20,  32,  41,  58,  25,  30,   0,
            -20,   6,   9,  49,  47,  35,  19,   9,
              3,  22,  24,  45,  57,  40,  57,  36,
            -18,  28,  19,  47,  31,  34,  39,  23,
            -16, -27,  15,   6,   9,  17,  10,   5,
            -22, -23, -30, -16, -16, -23, -36, -32,
            -33, -28, -22, -43,  -5, -32, -20, -41,
        };
        private static readonly int[] MgKing =
        {
            -65,  23,  16, -15, -56, -34,   2,  13,
             29,  -1, -20,  -7,  -8,  -4, -38, -29,
             -9,  24,   2, -16, -20,   6,  22, -22,
            -17, -20, -12, -27, -30, -25, -14, -36,
            -49,  -1, -27, -39, -46, -44, -33, -51,
            -14, -14, -22, -46, -44, -30, -15, -27,
              1,   7,  -8, -64, -43, -16,   9,   8,
            -15,  36,  12, -54,   8, -28,  24,  14,
        };
        private static readonly int[] EgKing =
        {
            -74, -35, -18, -18, -11,  15,   4, -17,
            -12,  17,  14,  17,  17,  38,  23,  11,
             10,  17,  23,  15,  20,  45,  44,  13,
             -8,  22,  24,  27,  26,  33,  26,   3,
            -18,  -4,  21,  24,  27,  23,   9, -11,
            -19,  -3,  11,  21,  23,  16,   7,  -9,
            -27, -11,   4,  13,  14,   4,  -5, -17,
            -53, -34, -21, -11, -28, -14, -24, -43,
        };

        // [piece, square] with material folded in, so the main loop is one lookup per piece.
        private static readonly int[,] Mg = new int[13, 64];
        private static readonly int[,] Eg = new int[13, 64];

        static Evaluation()
        {
            int[][] mgTables = { MgPawn, MgKnight, MgBishop, MgRook, MgQueen, MgKing };
            int[][] egTables = { EgPawn, EgKnight, EgBishop, EgRook, EgQueen, EgKing };

            for (int type = 0; type < 6; type++)
            {
                int white = (int)Pieces.Make(type, true);
                int black = (int)Pieces.Make(type, false);
                for (int sq = 0; sq < 64; sq++)
                {
                    Mg[white, sq] = MgValue[type] + mgTables[type][sq];
                    Eg[white, sq] = EgValue[type] + egTables[type][sq];
                    Mg[black, sq] = MgValue[type] + mgTables[type][sq ^ 56];
                    Eg[black, sq] = EgValue[type] + egTables[type][sq ^ 56];
                }
            }
        }

        public static int Evaluate(Board board)
        {
            Piece[] squares = board.Squares;
            int mg = 0, eg = 0, phase = 0;      // white minus black
            int whiteBishops = 0, blackBishops = 0;
            int whiteMaterial = 0, blackMaterial = 0; // non-pawn material in pawn units (N/B=3, R=5, Q=9)

            Span<int> whitePawnFiles = stackalloc int[8];
            Span<int> blackPawnFiles = stackalloc int[8];
            Span<int> whiteRearmost = stackalloc int[8]; // highest rank index of a white pawn on each file
            Span<int> blackRearmost = stackalloc int[8]; // lowest rank index of a black pawn on each file
            Span<int> pawnSquares = stackalloc int[16];
            Span<int> rookSquares = stackalloc int[20];
            int pawnCount = 0, rookCount = 0;
            whiteRearmost.Fill(-1);
            blackRearmost.Fill(8);

            for (int sq = 0; sq < 64; sq++)
            {
                Piece p = squares[sq];
                if (p == Piece.Empty) continue;

                int type = Pieces.Type(p);
                bool white = Pieces.IsWhite(p);
                phase += PhaseInc[type];

                if (white)
                {
                    mg += Mg[(int)p, sq];
                    eg += Eg[(int)p, sq];
                }
                else
                {
                    mg -= Mg[(int)p, sq];
                    eg -= Eg[(int)p, sq];
                }

                switch (type)
                {
                    case Pieces.Pawn:
                        int file = sq & 7, rank = sq >> 3;
                        if (white)
                        {
                            whitePawnFiles[file]++;
                            whiteRearmost[file] = Math.Max(whiteRearmost[file], rank);
                        }
                        else
                        {
                            blackPawnFiles[file]++;
                            blackRearmost[file] = Math.Min(blackRearmost[file], rank);
                        }
                        if (pawnCount < 16) pawnSquares[pawnCount++] = sq;
                        break;
                    case Pieces.Knight:
                        if (white) whiteMaterial += 3; else blackMaterial += 3;
                        break;
                    case Pieces.Bishop:
                        if (white) { whiteMaterial += 3; whiteBishops++; }
                        else { blackMaterial += 3; blackBishops++; }
                        break;
                    case Pieces.Rook:
                        if (white) whiteMaterial += 5; else blackMaterial += 5;
                        if (rookCount < 20) rookSquares[rookCount++] = sq;
                        break;
                    case Pieces.Queen:
                        if (white) whiteMaterial += 9; else blackMaterial += 9;
                        break;
                }
            }

            int whitePawns = 0, blackPawns = 0;
            for (int f = 0; f < 8; f++)
            {
                whitePawns += whitePawnFiles[f];
                blackPawns += blackPawnFiles[f];
            }

            // Bare kings or a single minor piece cannot mate.
            if (whitePawns == 0 && blackPawns == 0 && whiteMaterial <= 3 && blackMaterial <= 3)
                return 0;

            // --- PAWN STRUCTURE ---
            for (int f = 0; f < 8; f++)
            {
                if (whitePawnFiles[f] > 1)
                {
                    mg -= DoubledPawnMg * (whitePawnFiles[f] - 1);
                    eg -= DoubledPawnEg * (whitePawnFiles[f] - 1);
                }
                if (blackPawnFiles[f] > 1)
                {
                    mg += DoubledPawnMg * (blackPawnFiles[f] - 1);
                    eg += DoubledPawnEg * (blackPawnFiles[f] - 1);
                }
            }

            for (int i = 0; i < pawnCount; i++)
            {
                int sq = pawnSquares[i];
                int file = sq & 7, rank = sq >> 3;
                bool white = squares[sq] == Piece.WP;
                Span<int> ownFiles = white ? whitePawnFiles : blackPawnFiles;

                bool isolated = (file == 0 || ownFiles[file - 1] == 0) && (file == 7 || ownFiles[file + 1] == 0);

                // Passed: no enemy pawn ahead on this or an adjacent file.
                bool passed = true;
                for (int f = Math.Max(0, file - 1); f <= Math.Min(7, file + 1) && passed; f++)
                {
                    if (white && blackRearmost[f] < rank) passed = false;
                    if (!white && whiteRearmost[f] > rank) passed = false;
                }

                int sign = white ? 1 : -1;
                if (isolated)
                {
                    mg -= sign * IsolatedPawnMg;
                    eg -= sign * IsolatedPawnEg;
                }
                if (passed)
                {
                    int advanced = white ? 7 - rank : rank;
                    mg += sign * PassedPawnMg[advanced];
                    eg += sign * PassedPawnEg[advanced];
                }
            }

            // --- PIECES ---
            if (whiteBishops >= 2) { mg += BishopPairMg; eg += BishopPairEg; }
            if (blackBishops >= 2) { mg -= BishopPairMg; eg -= BishopPairEg; }

            for (int i = 0; i < rookCount; i++)
            {
                int sq = rookSquares[i];
                int file = sq & 7;
                bool white = squares[sq] == Piece.WR;
                int own = white ? whitePawnFiles[file] : blackPawnFiles[file];
                int enemy = white ? blackPawnFiles[file] : whitePawnFiles[file];
                int bonus = own > 0 ? 0 : enemy == 0 ? RookOpenFileMg : RookSemiOpenFileMg;
                mg += white ? bonus : -bonus;
            }

            int mgPhase = Math.Min(phase, MaxPhase);
            int score = (mg * mgPhase + eg * (MaxPhase - mgPhase)) / MaxPhase;

            // Without pawns, being up only a minor piece (e.g. R vs B) is usually a draw.
            if (score > 0 && whitePawns == 0 && whiteMaterial - blackMaterial <= 3) score /= 4;
            if (score < 0 && blackPawns == 0 && blackMaterial - whiteMaterial <= 3) score /= 4;

            // Mop-up: help the winning side drive a lone king to the edge and deliver mate.
            if (mgPhase <= 8)
            {
                if (score > 200 && blackPawns == 0 && blackMaterial <= 3)
                    score += MopUp(board.KingSquare[0], board.KingSquare[1]);
                else if (score < -200 && whitePawns == 0 && whiteMaterial <= 3)
                    score -= MopUp(board.KingSquare[1], board.KingSquare[0]);
            }

            return (board.WhiteToMove ? score : -score) + Tempo;
        }

        private static int MopUp(int winnerKing, int loserKing)
        {
            int lr = loserKing >> 3, lf = loserKing & 7;
            int centerDistance = Math.Max(3 - lr, lr - 4) + Math.Max(3 - lf, lf - 4);
            int kingDistance = Math.Abs(lr - (winnerKing >> 3)) + Math.Abs(lf - (winnerKing & 7));
            return 10 * centerDistance + 4 * (14 - kingDistance);
        }
    }
}

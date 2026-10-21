namespace Chess_Engine_2DArray_C_
{
    // Generates pseudo-legal moves (the king may be left in check). The search makes each move
    // and rejects it with Board.LastMoveWasIllegal(), which is much cheaper than filtering up front.
    public static class MoveGenerator
    {
        public const int MaxMoves = 256;

        public static int GenerateMoves(Board board, Move[] moves, bool capturesOnly = false)
        {
            int n = 0;
            bool white = board.WhiteToMove;
            Piece[] squares = board.Squares;

            for (int sq = 0; sq < 64; sq++)
            {
                Piece piece = squares[sq];
                if (piece == Piece.Empty || Pieces.IsWhite(piece) != white)
                    continue;

                switch (Pieces.Type(piece))
                {
                    case Pieces.Pawn:
                        n = GeneratePawnMoves(board, sq, white, moves, n, capturesOnly);
                        break;
                    case Pieces.Knight:
                        n = GenerateLeaperMoves(squares, sq, white, Attacks.Knight[sq], moves, n, capturesOnly);
                        break;
                    case Pieces.Bishop:
                        n = GenerateSlidingMoves(squares, sq, white, 4, 8, moves, n, capturesOnly);
                        break;
                    case Pieces.Rook:
                        n = GenerateSlidingMoves(squares, sq, white, 0, 4, moves, n, capturesOnly);
                        break;
                    case Pieces.Queen:
                        n = GenerateSlidingMoves(squares, sq, white, 0, 8, moves, n, capturesOnly);
                        break;
                    case Pieces.King:
                        n = GenerateLeaperMoves(squares, sq, white, Attacks.King[sq], moves, n, capturesOnly);
                        if (!capturesOnly)
                            n = GenerateCastlingMoves(board, white, moves, n);
                        break;
                }
            }

            return n;
        }

        public static List<Move> GenerateLegalMoves(Board board)
        {
            var moves = new Move[MaxMoves];
            int count = GenerateMoves(board, moves);
            var legal = new List<Move>(count);
            for (int i = 0; i < count; i++)
            {
                board.MakeMove(moves[i]);
                if (!board.LastMoveWasIllegal())
                    legal.Add(moves[i]);
                board.UnmakeMove();
            }
            return legal;
        }

        private static int GeneratePawnMoves(Board board, int sq, bool white, Move[] moves, int n, bool capturesOnly)
        {
            Piece[] squares = board.Squares;
            int direction = white ? -8 : 8;
            int startRank = white ? 6 : 1;
            int promotionRank = white ? 0 : 7;
            int file = sq & 7;
            int oneStep = sq + direction;
            bool promotes = (oneStep >> 3) == promotionRank;

            // --- FORWARD MOVE --- (queen promotions count as "captures" for quiescence)
            if (squares[oneStep] == Piece.Empty)
            {
                if (promotes)
                {
                    n = AddPromotions(sq, oneStep, white, moves, n, capturesOnly);
                }
                else if (!capturesOnly)
                {
                    moves[n++] = new Move(sq, oneStep);
                    int twoStep = oneStep + direction;
                    if ((sq >> 3) == startRank && squares[twoStep] == Piece.Empty)
                        moves[n++] = new Move(sq, twoStep, Piece.Empty, Move.FlagDoublePush);
                }
            }

            // --- DIAGONAL CAPTURES + EN PASSANT ---
            for (int df = -1; df <= 1; df += 2)
            {
                if (file + df < 0 || file + df > 7) continue;
                int target = oneStep + df;
                Piece victim = squares[target];

                if (victim != Piece.Empty && Pieces.IsWhite(victim) != white)
                {
                    if (promotes)
                        n = AddPromotions(sq, target, white, moves, n, capturesOnly);
                    else
                        moves[n++] = new Move(sq, target);
                }
                else if (target == board.EnPassantSquare)
                {
                    moves[n++] = new Move(sq, target, Piece.Empty, Move.FlagEnPassant);
                }
            }

            return n;
        }

        private static int AddPromotions(int from, int to, bool white, Move[] moves, int n, bool queenOnly)
        {
            moves[n++] = new Move(from, to, white ? Piece.WQ : Piece.BQ);
            if (queenOnly) return n;
            moves[n++] = new Move(from, to, white ? Piece.WR : Piece.BR);
            moves[n++] = new Move(from, to, white ? Piece.WB : Piece.BB);
            moves[n++] = new Move(from, to, white ? Piece.WN : Piece.BN);
            return n;
        }

        private static int GenerateLeaperMoves(Piece[] squares, int sq, bool white, int[] targets, Move[] moves, int n, bool capturesOnly)
        {
            foreach (int t in targets)
            {
                Piece p = squares[t];
                if (p == Piece.Empty)
                {
                    if (!capturesOnly) moves[n++] = new Move(sq, t);
                }
                else if (Pieces.IsWhite(p) != white)
                {
                    moves[n++] = new Move(sq, t);
                }
            }
            return n;
        }

        private static int GenerateSlidingMoves(Piece[] squares, int sq, bool white, int firstDir, int lastDir, Move[] moves, int n, bool capturesOnly)
        {
            int[][] rays = Attacks.Rays[sq];
            for (int d = firstDir; d < lastDir; d++)
            {
                foreach (int t in rays[d])
                {
                    Piece p = squares[t];
                    if (p == Piece.Empty)
                    {
                        if (!capturesOnly) moves[n++] = new Move(sq, t);
                        continue;
                    }
                    if (Pieces.IsWhite(p) != white)
                        moves[n++] = new Move(sq, t);
                    break;
                }
            }
            return n;
        }

        private static int GenerateCastlingMoves(Board board, bool white, Move[] moves, int n)
        {
            Piece[] s = board.Squares;
            int rights = board.CastlingRights;

            if (white)
            {
                if (s[60] != Piece.WK) return n;
                if ((rights & Board.WhiteKingside) != 0 && s[61] == Piece.Empty && s[62] == Piece.Empty && s[63] == Piece.WR &&
                    !board.IsSquareAttacked(60, false) && !board.IsSquareAttacked(61, false) && !board.IsSquareAttacked(62, false))
                    moves[n++] = new Move(60, 62, Piece.Empty, Move.FlagCastle);
                if ((rights & Board.WhiteQueenside) != 0 && s[59] == Piece.Empty && s[58] == Piece.Empty && s[57] == Piece.Empty && s[56] == Piece.WR &&
                    !board.IsSquareAttacked(60, false) && !board.IsSquareAttacked(59, false) && !board.IsSquareAttacked(58, false))
                    moves[n++] = new Move(60, 58, Piece.Empty, Move.FlagCastle);
            }
            else
            {
                if (s[4] != Piece.BK) return n;
                if ((rights & Board.BlackKingside) != 0 && s[5] == Piece.Empty && s[6] == Piece.Empty && s[7] == Piece.BR &&
                    !board.IsSquareAttacked(4, true) && !board.IsSquareAttacked(5, true) && !board.IsSquareAttacked(6, true))
                    moves[n++] = new Move(4, 6, Piece.Empty, Move.FlagCastle);
                if ((rights & Board.BlackQueenside) != 0 && s[3] == Piece.Empty && s[2] == Piece.Empty && s[1] == Piece.Empty && s[0] == Piece.BR &&
                    !board.IsSquareAttacked(4, true) && !board.IsSquareAttacked(3, true) && !board.IsSquareAttacked(2, true))
                    moves[n++] = new Move(4, 2, Piece.Empty, Move.FlagCastle);
            }

            return n;
        }

        public static long Perft(Board board, int depth)
        {
            if (depth == 0) return 1;

            var moves = new Move[MaxMoves];
            int count = GenerateMoves(board, moves);
            long nodes = 0;
            for (int i = 0; i < count; i++)
            {
                board.MakeMove(moves[i]);
                if (!board.LastMoveWasIllegal())
                    nodes += depth == 1 ? 1 : Perft(board, depth - 1);
                board.UnmakeMove();
            }
            return nodes;
        }
    }
}

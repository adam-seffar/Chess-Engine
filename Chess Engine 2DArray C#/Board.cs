using System.Text;

namespace Chess_Engine_2DArray_C_
{
    public enum Piece : byte
    {
        Empty,
        WP, WN, WB, WR, WQ, WK,
        BP, BN, BB, BR, BQ, BK
    }

    public static class Pieces
    {
        public const int Pawn = 0, Knight = 1, Bishop = 2, Rook = 3, Queen = 4, King = 5;

        public static bool IsWhite(Piece p) => p >= Piece.WP && p <= Piece.WK;
        public static bool IsBlack(Piece p) => p >= Piece.BP;

        // 0 = pawn ... 5 = king, regardless of color. Undefined for Empty.
        public static int Type(Piece p) => ((int)p - 1) % 6;
        public static Piece Make(int type, bool white) => (Piece)(type + (white ? 1 : 7));

        public static char ToChar(Piece p) => ".PNBRQKpnbrqk"[(int)p];

        public static Piece FromChar(char c)
        {
            int i = "PNBRQKpnbrqk".IndexOf(c);
            return i < 0 ? Piece.Empty : (Piece)(i + 1);
        }
    }

    public static class Zobrist
    {
        public static readonly ulong[] PieceSquare = new ulong[13 * 64];
        public static readonly ulong[] Castling = new ulong[16];
        public static readonly ulong[] EnPassantFile = new ulong[8];
        public static readonly ulong SideToMove;

        static Zobrist()
        {
            // Fixed-seed xorshift so hashes are reproducible between runs.
            ulong state = 0x9E3779B97F4A7C15UL;
            ulong Next()
            {
                state ^= state << 13;
                state ^= state >> 7;
                state ^= state << 17;
                return state;
            }

            for (int i = 64; i < PieceSquare.Length; i++) PieceSquare[i] = Next();
            for (int i = 1; i < 16; i++) Castling[i] = Next();
            for (int i = 0; i < 8; i++) EnPassantFile[i] = Next();
            SideToMove = Next();
        }

        public static ulong Piece(Piece p, int sq) => PieceSquare[(int)p * 64 + sq];
    }

    public sealed class Board
    {
        public const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        public const int WhiteKingside = 1, WhiteQueenside = 2, BlackKingside = 4, BlackQueenside = 8;

        // squares[rank * 8 + file], rank 0 = 8th rank (black's back rank), like the old Piece[8, 8].
        public readonly Piece[] Squares = new Piece[64];
        public bool WhiteToMove = true;
        public int CastlingRights;
        public int EnPassantSquare = -1;
        public int HalfmoveClock;
        public int FullmoveNumber = 1;
        public ulong Key;
        public readonly int[] KingSquare = new int[2]; // [0] white, [1] black

        // Number of moves made since the position was loaded; indexes the undo stack.
        public int Ply;

        private struct Undo
        {
            public Move Move;
            public Piece Captured;
            public int Castling;
            public int EnPassant;
            public int Halfmove;
            public ulong Key;
        }

        private Undo[] history = new Undo[1024];

        // Moving from or to these squares clears the matching castling rights.
        private static readonly int[] CastleMask = BuildCastleMask();

        private static int[] BuildCastleMask()
        {
            int[] mask = new int[64];
            Array.Fill(mask, 15);
            mask[56] &= ~WhiteQueenside;                    // a1
            mask[63] &= ~WhiteKingside;                     // h1
            mask[60] &= ~(WhiteKingside | WhiteQueenside);  // e1
            mask[0] &= ~BlackQueenside;                     // a8
            mask[7] &= ~BlackKingside;                      // h8
            mask[4] &= ~(BlackKingside | BlackQueenside);   // e8
            return mask;
        }

        public Board()
        {
            LoadFromFen(StartFen);
        }

        public Board Clone()
        {
            var copy = new Board();
            copy.LoadFromFen(ToFen());
            return copy;
        }

        //FEN

        public void LoadFromFen(string fen)
        {
            string[] parts = fen.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
                throw new ArgumentException("Invalid FEN string - too few parts");

            Array.Clear(Squares);
            string[] ranks = parts[0].Split('/');
            if (ranks.Length != 8)
                throw new ArgumentException("Invalid FEN - must have 8 ranks");

            for (int rank = 0; rank < 8; rank++)
            {
                int file = 0;
                foreach (char c in ranks[rank])
                {
                    if (char.IsDigit(c))
                    {
                        file += c - '0';
                    }
                    else
                    {
                        if (file > 7) throw new ArgumentException($"Invalid FEN - rank {8 - rank} is too long");
                        Piece p = Pieces.FromChar(c);
                        if (p == Piece.Empty) throw new ArgumentException($"Invalid FEN piece: {c}");
                        Squares[rank * 8 + file] = p;
                        if (p == Piece.WK) KingSquare[0] = rank * 8 + file;
                        if (p == Piece.BK) KingSquare[1] = rank * 8 + file;
                        file++;
                    }
                }
                if (file != 8)
                    throw new ArgumentException($"Invalid FEN - rank {8 - rank} has {file} files");
            }

            WhiteToMove = parts[1] == "w";

            CastlingRights = 0;
            foreach (char c in parts[2])
            {
                switch (c)
                {
                    case 'K': CastlingRights |= WhiteKingside; break;
                    case 'Q': CastlingRights |= WhiteQueenside; break;
                    case 'k': CastlingRights |= BlackKingside; break;
                    case 'q': CastlingRights |= BlackQueenside; break;
                }
            }

            EnPassantSquare = parts[3] == "-" ? -1 : ParseSquare(parts[3]);
            HalfmoveClock = parts.Length > 4 ? int.Parse(parts[4]) : 0;
            FullmoveNumber = parts.Length > 5 ? int.Parse(parts[5]) : 1;

            Ply = 0;
            Key = ComputeKey();
        }

        public string ToFen()
        {
            var sb = new StringBuilder();
            for (int rank = 0; rank < 8; rank++)
            {
                int empty = 0;
                for (int file = 0; file < 8; file++)
                {
                    Piece p = Squares[rank * 8 + file];
                    if (p == Piece.Empty)
                    {
                        empty++;
                        continue;
                    }
                    if (empty > 0) sb.Append(empty);
                    empty = 0;
                    sb.Append(Pieces.ToChar(p));
                }
                if (empty > 0) sb.Append(empty);
                if (rank < 7) sb.Append('/');
            }

            sb.Append(WhiteToMove ? " w " : " b ");

            if (CastlingRights == 0) sb.Append('-');
            if ((CastlingRights & WhiteKingside) != 0) sb.Append('K');
            if ((CastlingRights & WhiteQueenside) != 0) sb.Append('Q');
            if ((CastlingRights & BlackKingside) != 0) sb.Append('k');
            if ((CastlingRights & BlackQueenside) != 0) sb.Append('q');

            sb.Append(' ').Append(EnPassantSquare < 0 ? "-" : SquareName(EnPassantSquare));
            sb.Append(' ').Append(HalfmoveClock).Append(' ').Append(FullmoveNumber);
            return sb.ToString();
        }

        public ulong ComputeKey()
        {
            ulong key = 0;
            for (int sq = 0; sq < 64; sq++)
                if (Squares[sq] != Piece.Empty)
                    key ^= Zobrist.Piece(Squares[sq], sq);
            key ^= Zobrist.Castling[CastlingRights];
            if (EnPassantSquare >= 0) key ^= Zobrist.EnPassantFile[EnPassantSquare & 7];
            if (!WhiteToMove) key ^= Zobrist.SideToMove;
            return key;
        }

        //PRINT

        public void PrintBoard()
        {
            for (int rank = 0; rank < 8; rank++)
            {
                Console.Write(8 - rank + " ");
                for (int file = 0; file < 8; file++)
                    Console.Write(Pieces.ToChar(Squares[rank * 8 + file]) + " ");
                Console.WriteLine();
            }
            Console.WriteLine("  a b c d e f g h");
        }

        //MAKE AND UNMAKE MOVE

        public void MakeMove(Move move)
        {
            int from = move.From, to = move.To, flag = move.Flag;
            Piece piece = Squares[from];
            Piece captured = Squares[to];

            if (Ply == history.Length) Array.Resize(ref history, history.Length * 2);
            ref Undo undo = ref history[Ply++];
            undo.Move = move;
            undo.Castling = CastlingRights;
            undo.EnPassant = EnPassantSquare;
            undo.Halfmove = HalfmoveClock;
            undo.Key = Key;

            if (EnPassantSquare >= 0) Key ^= Zobrist.EnPassantFile[EnPassantSquare & 7];
            EnPassantSquare = -1;
            Key ^= Zobrist.Castling[CastlingRights];

            if (flag == Move.FlagEnPassant)
            {
                // The captured pawn sits beside the moving pawn, not on the target square.
                int capturedSquare = WhiteToMove ? to + 8 : to - 8;
                captured = Squares[capturedSquare];
                Squares[capturedSquare] = Piece.Empty;
                Key ^= Zobrist.Piece(captured, capturedSquare);
            }
            else if (captured != Piece.Empty)
            {
                Key ^= Zobrist.Piece(captured, to);
            }
            undo.Captured = captured;

            Piece placed = move.Promotion != Piece.Empty ? move.Promotion : piece;
            Squares[from] = Piece.Empty;
            Squares[to] = placed;
            Key ^= Zobrist.Piece(piece, from) ^ Zobrist.Piece(placed, to);

            if (flag == Move.FlagCastle)
            {
                GetCastleRookSquares(to, out int rookFrom, out int rookTo);
                Piece rook = Squares[rookFrom];
                Squares[rookFrom] = Piece.Empty;
                Squares[rookTo] = rook;
                Key ^= Zobrist.Piece(rook, rookFrom) ^ Zobrist.Piece(rook, rookTo);
            }

            if (piece == Piece.WK) KingSquare[0] = to;
            else if (piece == Piece.BK) KingSquare[1] = to;

            CastlingRights &= CastleMask[from] & CastleMask[to];
            Key ^= Zobrist.Castling[CastlingRights];

            if (flag == Move.FlagDoublePush)
            {
                EnPassantSquare = (from + to) / 2;
                Key ^= Zobrist.EnPassantFile[EnPassantSquare & 7];
            }

            if (piece == Piece.WP || piece == Piece.BP || captured != Piece.Empty)
                HalfmoveClock = 0;
            else
                HalfmoveClock++;

            if (!WhiteToMove) FullmoveNumber++;
            WhiteToMove = !WhiteToMove;
            Key ^= Zobrist.SideToMove;
        }

        public void UnmakeMove()
        {
            ref Undo undo = ref history[--Ply];
            Move move = undo.Move;
            int from = move.From, to = move.To;

            WhiteToMove = !WhiteToMove;
            if (!WhiteToMove) FullmoveNumber--;

            Piece placed = Squares[to];
            Piece piece = move.Promotion != Piece.Empty ? (WhiteToMove ? Piece.WP : Piece.BP) : placed;
            Squares[from] = piece;
            Squares[to] = Piece.Empty;

            if (move.Flag == Move.FlagEnPassant)
                Squares[WhiteToMove ? to + 8 : to - 8] = undo.Captured;
            else
                Squares[to] = undo.Captured;

            if (move.Flag == Move.FlagCastle)
            {
                GetCastleRookSquares(to, out int rookFrom, out int rookTo);
                Squares[rookFrom] = Squares[rookTo];
                Squares[rookTo] = Piece.Empty;
            }

            if (piece == Piece.WK) KingSquare[0] = from;
            else if (piece == Piece.BK) KingSquare[1] = from;

            CastlingRights = undo.Castling;
            EnPassantSquare = undo.EnPassant;
            HalfmoveClock = undo.Halfmove;
            Key = undo.Key;
        }

        // Passing the turn, used by null-move pruning in the search.
        public void MakeNullMove()
        {
            if (Ply == history.Length) Array.Resize(ref history, history.Length * 2);
            ref Undo undo = ref history[Ply++];
            undo.Move = Move.None;
            undo.Captured = Piece.Empty;
            undo.Castling = CastlingRights;
            undo.EnPassant = EnPassantSquare;
            undo.Halfmove = HalfmoveClock;
            undo.Key = Key;

            if (EnPassantSquare >= 0) Key ^= Zobrist.EnPassantFile[EnPassantSquare & 7];
            EnPassantSquare = -1;
            HalfmoveClock = 0; // stops repetition detection looking past the null move
            WhiteToMove = !WhiteToMove;
            Key ^= Zobrist.SideToMove;
        }

        public void UnmakeNullMove()
        {
            ref Undo undo = ref history[--Ply];
            WhiteToMove = !WhiteToMove;
            EnPassantSquare = undo.EnPassant;
            HalfmoveClock = undo.Halfmove;
            Key = undo.Key;
        }

        private static void GetCastleRookSquares(int kingTo, out int rookFrom, out int rookTo)
        {
            switch (kingTo)
            {
                case 62: rookFrom = 63; rookTo = 61; break; // white O-O
                case 58: rookFrom = 56; rookTo = 59; break; // white O-O-O
                case 6: rookFrom = 7; rookTo = 5; break;    // black O-O
                default: rookFrom = 0; rookTo = 3; break;   // black O-O-O
            }
        }

        // CHECK, ATTACK AND DRAW LOGIC

        public bool IsSquareAttacked(int sq, bool byWhite)
        {
            Piece[] s = Squares;
            int rank = sq >> 3, file = sq & 7;

            // Pawns: white pawns attack toward rank 0, so they sit one rank "below" (higher index).
            if (byWhite)
            {
                if (rank < 7)
                {
                    if (file > 0 && s[sq + 7] == Piece.WP) return true;
                    if (file < 7 && s[sq + 9] == Piece.WP) return true;
                }
            }
            else if (rank > 0)
            {
                if (file > 0 && s[sq - 9] == Piece.BP) return true;
                if (file < 7 && s[sq - 7] == Piece.BP) return true;
            }

            Piece knight = byWhite ? Piece.WN : Piece.BN;
            foreach (int t in Attacks.Knight[sq])
                if (s[t] == knight) return true;

            Piece king = byWhite ? Piece.WK : Piece.BK;
            foreach (int t in Attacks.King[sq])
                if (s[t] == king) return true;

            Piece queen = byWhite ? Piece.WQ : Piece.BQ;
            Piece rook = byWhite ? Piece.WR : Piece.BR;
            Piece bishop = byWhite ? Piece.WB : Piece.BB;
            int[][] rays = Attacks.Rays[sq];
            for (int d = 0; d < 8; d++)
            {
                Piece slider = d < 4 ? rook : bishop;
                foreach (int t in rays[d])
                {
                    Piece p = s[t];
                    if (p == Piece.Empty) continue;
                    if (p == slider || p == queen) return true;
                    break;
                }
            }

            return false;
        }

        public bool InCheck()
        {
            return IsSquareAttacked(KingSquare[WhiteToMove ? 0 : 1], !WhiteToMove);
        }

        // True if the side that just moved left its own king in check.
        public bool LastMoveWasIllegal()
        {
            return IsSquareAttacked(KingSquare[WhiteToMove ? 1 : 0], WhiteToMove);
        }

        // Any earlier occurrence of this position since the last capture or pawn move.
        public bool IsRepetition()
        {
            int stop = Math.Max(0, Ply - HalfmoveClock);
            for (int i = Ply - 4; i >= stop; i -= 2)
                if (history[i].Key == Key)
                    return true;
            return false;
        }

        public int RepetitionCount()
        {
            int count = 1;
            int stop = Math.Max(0, Ply - HalfmoveClock);
            for (int i = Ply - 4; i >= stop; i -= 2)
                if (history[i].Key == Key)
                    count++;
            return count;
        }

        // Neither side can ever mate: bare kings, or a single knight or bishop on the board.
        public bool IsInsufficientMaterial()
        {
            int minors = 0;
            foreach (Piece p in Squares)
            {
                if (p == Piece.Empty || p == Piece.WK || p == Piece.BK) continue;
                int type = Pieces.Type(p);
                if (type != Pieces.Knight && type != Pieces.Bishop) return false;
                minors++;
            }
            return minors <= 1;
        }

        public bool HasNonPawnMaterial(bool white)
        {
            int lo = white ? (int)Piece.WN : (int)Piece.BN;
            int hi = white ? (int)Piece.WQ : (int)Piece.BQ;
            foreach (Piece p in Squares)
                if ((int)p >= lo && (int)p <= hi)
                    return true;
            return false;
        }

        //HELPER FUNCTIONS

        public static string SquareName(int sq)
        {
            return $"{(char)('a' + (sq & 7))}{8 - (sq >> 3)}";
        }

        public static int ParseSquare(string s)
        {
            int file = char.ToLower(s[0]) - 'a';
            int rank = 8 - (s[1] - '0');
            if (file < 0 || file > 7 || rank < 0 || rank > 7)
                throw new ArgumentException($"Invalid square: {s}");
            return rank * 8 + file;
        }
    }
}

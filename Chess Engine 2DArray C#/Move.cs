namespace Chess_Engine_2DArray_C_
{
    // A move packed into one int so move lists are cheap to copy and compare:
    // bits 0-5 from square, 6-11 to square, 12-15 promotion piece, 16-18 flag.
    // Squares are rank * 8 + file with rank 0 = the 8th rank (a8 = 0, h1 = 63), same as the 8x8 layout.
    public readonly struct Move : IEquatable<Move>
    {
        public const int FlagNone = 0;
        public const int FlagDoublePush = 1;
        public const int FlagEnPassant = 2;
        public const int FlagCastle = 3;

        public static readonly Move None = default;

        public readonly int Value;

        public Move(int value)
        {
            Value = value;
        }

        public Move(int from, int to, Piece promotion = Piece.Empty, int flag = FlagNone)
        {
            Value = from | (to << 6) | ((int)promotion << 12) | (flag << 16);
        }

        public int From => Value & 63;
        public int To => (Value >> 6) & 63;
        public Piece Promotion => (Piece)((Value >> 12) & 15);
        public int Flag => (Value >> 16) & 7;
        public bool IsNone => Value == 0;

        public int FromRank => From >> 3;
        public int FromFile => From & 7;
        public int ToRank => To >> 3;
        public int ToFile => To & 7;

        public bool Equals(Move other) => Value == other.Value;
        public override bool Equals(object? obj) => obj is Move m && m.Value == Value;
        public override int GetHashCode() => Value;
        public static bool operator ==(Move a, Move b) => a.Value == b.Value;
        public static bool operator !=(Move a, Move b) => a.Value != b.Value;

        // UCI long algebraic notation, e.g. "e2e4" or "e7e8q".
        public override string ToString()
        {
            if (IsNone) return "0000";
            string s = Board.SquareName(From) + Board.SquareName(To);
            if (Promotion != Piece.Empty)
                s += char.ToLower(Pieces.ToChar(Promotion));
            return s;
        }
    }
}

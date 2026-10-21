namespace Chess_Engine_2DArray_C_
{
    // Precomputed target squares for every square, so move generation and attack
    // detection never need bounds checks.
    public static class Attacks
    {
        public static readonly int[][] Knight = new int[64][];
        public static readonly int[][] King = new int[64][];

        // Rays[square][direction] lists squares outward from the square, nearest first.
        // Directions 0-3 are orthogonal (rook), 4-7 are diagonal (bishop).
        public static readonly int[][][] Rays = new int[64][][];

        private static readonly (int dr, int df)[] Directions =
        {
            (-1, 0), (1, 0), (0, -1), (0, 1),
            (-1, -1), (-1, 1), (1, -1), (1, 1)
        };

        private static readonly (int dr, int df)[] KnightOffsets =
        {
            (-2, -1), (-2, 1), (-1, -2), (-1, 2),
            (1, -2), (1, 2), (2, -1), (2, 1)
        };

        static Attacks()
        {
            for (int sq = 0; sq < 64; sq++)
            {
                int rank = sq >> 3, file = sq & 7;

                var knight = new List<int>();
                foreach (var (dr, df) in KnightOffsets)
                    if (InBounds(rank + dr, file + df))
                        knight.Add((rank + dr) * 8 + file + df);
                Knight[sq] = knight.ToArray();

                var king = new List<int>();
                foreach (var (dr, df) in Directions)
                    if (InBounds(rank + dr, file + df))
                        king.Add((rank + dr) * 8 + file + df);
                King[sq] = king.ToArray();

                Rays[sq] = new int[8][];
                for (int d = 0; d < 8; d++)
                {
                    var ray = new List<int>();
                    int r = rank + Directions[d].dr, f = file + Directions[d].df;
                    while (InBounds(r, f))
                    {
                        ray.Add(r * 8 + f);
                        r += Directions[d].dr;
                        f += Directions[d].df;
                    }
                    Rays[sq][d] = ray.ToArray();
                }
            }
        }

        private static bool InBounds(int rank, int file)
        {
            return rank >= 0 && rank < 8 && file >= 0 && file < 8;
        }
    }
}

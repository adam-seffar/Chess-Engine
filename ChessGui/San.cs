using Chess_Engine_2DArray_C_;

namespace ChessGui
{
    // Standard algebraic notation (e.g. "Nbd7", "exd5", "O-O", "e8=Q+") for the move list.
    internal static class San
    {
        public static string ToSan(Board board, Move move)
        {
            string san;
            if (move.Flag == Move.FlagCastle)
            {
                san = (move.To & 7) == 6 ? "O-O" : "O-O-O";
            }
            else
            {
                Piece piece = board.Squares[move.From];
                int type = Pieces.Type(piece);
                bool capture = board.Squares[move.To] != Piece.Empty || move.Flag == Move.FlagEnPassant;
                string to = Board.SquareName(move.To);

                if (type == Pieces.Pawn)
                {
                    san = capture ? $"{(char)('a' + move.FromFile)}x{to}" : to;
                    if (move.Promotion != Piece.Empty)
                        san += "=" + char.ToUpper(Pieces.ToChar(move.Promotion));
                }
                else
                {
                    // Disambiguate when another piece of the same kind can reach the same square.
                    var rivals = MoveGenerator.GenerateLegalMoves(board)
                        .Where(m => m.To == move.To && m.From != move.From && board.Squares[m.From] == piece)
                        .ToList();
                    string from = "";
                    if (rivals.Count > 0)
                    {
                        string square = Board.SquareName(move.From);
                        if (rivals.All(m => m.FromFile != move.FromFile)) from = square[..1];
                        else if (rivals.All(m => m.FromRank != move.FromRank)) from = square[1..];
                        else from = square;
                    }
                    san = "NBRQK"[type - 1] + from + (capture ? "x" : "") + to;
                }
            }

            board.MakeMove(move);
            if (board.InCheck())
                san += MoveGenerator.GenerateLegalMoves(board).Count == 0 ? "#" : "+";
            board.UnmakeMove();
            return san;
        }
    }
}

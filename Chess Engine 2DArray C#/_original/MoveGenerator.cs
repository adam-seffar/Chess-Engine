using System;
using System.Collections.Generic;

namespace Chess_Engine_2DArray_C_
{
    public class MoveGenerator
    {
        public List<Move> GenerateMoves(Board board, bool whiteToMove)
        {
            List<Move> moves = new List<Move>();

            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    Piece piece = board.squares[rank, file];

                    if (piece == Piece.Empty || IsWhite(piece) != whiteToMove)
                        continue;

                    switch (piece)
                    {
                        case Piece.WP:
                        case Piece.BP:
                            moves.AddRange(GeneratePawnMoves(board, file, rank, whiteToMove));
                            break;
                        case Piece.WN:
                        case Piece.BN:
                            moves.AddRange(GenerateKnightMoves(board, file, rank, whiteToMove));
                            break;
                        case Piece.WB:
                        case Piece.BB:
                            moves.AddRange(GenerateSlidingMoves(board, file, rank, whiteToMove,
                                new (int, int)[] { (-1, -1), (-1, 1), (1, -1), (1, 1) }));
                            break;
                        case Piece.WR:
                        case Piece.BR:
                            moves.AddRange(GenerateSlidingMoves(board, file, rank, whiteToMove,
                                new (int, int)[] { (-1, 0), (1, 0), (0, -1), (0, 1) }));
                            break;
                        case Piece.WQ:
                        case Piece.BQ:
                            moves.AddRange(GenerateSlidingMoves(board, file, rank, whiteToMove,
                                new (int, int)[] {
                                    (-1, 0), (1, 0), (0, -1), (0, 1),
                                    (-1, -1), (-1, 1), (1, -1), (1, 1)
                                }));
                            break;
                        case Piece.WK:
                        case Piece.BK:
                            moves.AddRange(GenerateKingMoves(board, file, rank, whiteToMove));
                            break;
                    }
                }
            }

            return moves;
        }

        // Original GeneratePawnMoves

        // private List<Move> GeneratePawnMoves(Board board, int file, int rank, bool whiteToMove)
        //{
        //    List<Move> moves = new List<Move>();
        //    int direction = whiteToMove ? -1 : 1;
        //    int nextRank = rank + direction;

        //    // Standard move forward
        //    if (board.squares[nextRank, file] == Piece.Empty)
        //    {
        //        if (nextRank == 0 || nextRank == 7)
        //        {
        //            // Promotion options
        //            moves.AddRange(PromotionMoves(file, rank, file, nextRank, whiteToMove));
        //        }
        //        else
        //        {
        //            moves.Add(new Move(file, rank, file, nextRank));
        //        }

        //        // Double move from starting rank
        //        if ((whiteToMove && rank == 6) || (!whiteToMove && rank == 1))
        //        {
        //            int twoStepRank = rank + 2 * direction;
        //            if (board.squares[twoStepRank, file] == Piece.Empty)
        //                moves.Add(new Move(file, rank, file, twoStepRank));
        //        }
        //    }

        //    // Captures (left and right)
        //    foreach (int df in new[] { -1, 1 })
        //    {
        //        int targetFile = file + df;
        //        if (targetFile < 0 || targetFile > 7)
        //            continue;

        //        Piece target = board.squares[nextRank, targetFile];
        //        if (target != Piece.Empty && IsWhite(target) != whiteToMove)
        //        {
        //            if (nextRank == 0 || nextRank == 7)
        //            {
        //                moves.AddRange(PromotionMoves(file, rank, targetFile, nextRank, whiteToMove));
        //            }
        //            else
        //            {
        //                moves.Add(new Move(file, rank, targetFile, nextRank));
        //            }
        //        }
        //    }

        //    // En passant captures
        //    if (board.EnPassantTargetSquare.HasValue)
        //    {
        //        var (epRank, epFile) = board.EnPassantTargetSquare.Value;

        //        if (nextRank == epRank && Math.Abs(file - epFile) == 1)
        //        {
        //            moves.Add(new Move(file, rank, epFile, epRank));
        //        }
        //    }


        //    return moves;
        //}

        private List<Move> GeneratePawnMoves(Board board, int file, int rank, bool whiteToMove)
        {
            List<Move> moves = new List<Move>();
            int direction = whiteToMove ? -1 : 1;
            int startRank = whiteToMove ? 6 : 1;
            int promotionRank = whiteToMove ? 0 : 7;
            int nextRank = rank + direction;

            if (!IsInBounds(nextRank, file)) return moves;

            // --- FORWARD MOVE ---
            if (board.squares[nextRank, file] == Piece.Empty)
            {
                if (nextRank == promotionRank)
                {
                    moves.AddRange(PromotionMoves(file, rank, file, nextRank, whiteToMove));
                }
                else
                {
                    moves.Add(new Move(file, rank, file, nextRank));

                    // Double move from starting rank
                    int twoStepRank = rank + 2 * direction;
                    if (rank == startRank && IsInBounds(twoStepRank, file) &&
                        board.squares[twoStepRank, file] == Piece.Empty)
                    {
                        moves.Add(new Move(file, rank, file, twoStepRank));
                    }
                }
            }

            // --- DIAGONAL CAPTURES + EN PASSANT ---
            foreach (int df in new[] { -1, 1 })
            {
                int targetFile = file + df;
                if (!IsInBounds(nextRank, targetFile)) continue;

                Piece target = board.squares[nextRank, targetFile];

                // Capture enemy piece
                if (target != Piece.Empty && IsWhite(target) != whiteToMove)
                {
                    if (nextRank == promotionRank)
                    {
                        moves.AddRange(PromotionMoves(file, rank, targetFile, nextRank, whiteToMove));
                    }
                    else
                    {
                        moves.Add(new Move(file, rank, targetFile, nextRank));
                    }
                }

                // En passant
                if (board.EnPassantTargetSquare.HasValue)
                {
                    var (epRank, epFile) = board.EnPassantTargetSquare.Value;
                    if (nextRank == epRank && targetFile == epFile)
                    {
                        moves.Add(new Move(file, rank, targetFile, nextRank));
                    }
                }
            }

            return moves;
        }

        private List<Move> PromotionMoves(int fromFile, int fromRank, int toFile, int toRank, bool whiteToMove)
        {
            return new List<Move>
            {
                new Move(fromFile, fromRank, toFile, toRank, whiteToMove ? Piece.WQ : Piece.BQ),
                new Move(fromFile, fromRank, toFile, toRank, whiteToMove ? Piece.WR : Piece.BR),
                new Move(fromFile, fromRank, toFile, toRank, whiteToMove ? Piece.WB : Piece.BB),
                new Move(fromFile, fromRank, toFile, toRank, whiteToMove ? Piece.WN : Piece.BN)
            };
        }

        private List<Move> GenerateKnightMoves(Board board, int file, int rank, bool white)
        {
            List<Move> moves = new List<Move>();
            Piece[,] squares = board.squares;

            int[,] offsets = new int[,]
            {
        {-2, -1}, {-2, 1}, {-1, -2}, {-1, 2},
        {1, -2}, {1, 2}, {2, -1}, {2, 1}
            };

            for (int i = 0; i < offsets.GetLength(0); i++)
            {
                int r = rank + offsets[i, 0];
                int f = file + offsets[i, 1];

                if (IsInBounds(r, f) && !IsFriendlyPiece(squares[r, f], white))
                    moves.Add(new Move(file, rank, f, r)); // Fixed parameter order
                                                           //          (fromFile, fromRank, toFile, toRank) ✅ Correct order!
            }

            return moves;
        }
        private List<Move> GenerateSlidingMoves(Board board, int file, int rank, bool white, (int, int)[] directions)
        {
            List<Move> moves = new List<Move>();
            Piece[,] squares = board.squares;

            foreach (var (dr, df) in directions)
            {
                int r = rank + dr;
                int f = file + df;

                while (IsInBounds(r, f))
                {
                    if (squares[r, f] == Piece.Empty)
                    {
                        moves.Add(new Move(rank, file, r, f, squares[rank, file]));
                    }
                    else
                    {
                        if (!IsFriendlyPiece(squares[r, f], white))
                            moves.Add(new Move(rank, file, r, f, squares[rank, file]));
                        break;
                    }

                    r += dr;
                    f += df;
                }
            }

            return moves;
        }

        private List<Move> GenerateKingMoves(Board board, int file, int rank, bool white)
        {
            List<Move> moves = new List<Move>();
            Piece[,] squares = board.squares;

            // Standard king moves
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int df = -1; df <= 1; df++)
                {
                    if (dr == 0 && df == 0) continue;

                    int r = rank + dr;
                    int f = file + df;

                    if (IsInBounds(r, f) && !IsFriendlyPiece(squares[r, f], white))
                    {
                        moves.Add(new Move(file, rank, f, r));
                    }
                }
            }

            // Castling moves
            int homeRank = white ? 7 : 0;
            if (rank == homeRank && file == 4) // King on starting position
            {
                // Kingside castling
                if ((white && board.Castling.WhiteKingside) ||
                    (!white && board.Castling.BlackKingside))
                {
                    bool rookPresent = squares[homeRank, 7] == (white ? Piece.WR : Piece.BR);

                    if (rookPresent && squares[homeRank, 5] == Piece.Empty &&
                        squares[homeRank, 6] == Piece.Empty &&
                        !IsSquareAttacked(board, homeRank, 4, !white) &&
                        !IsSquareAttacked(board, homeRank, 5, !white) &&
                        !IsSquareAttacked(board, homeRank, 6, !white))
                    {
                        moves.Add(new Move(4, homeRank, 6, homeRank));
                    }
                }

                // Queenside castling
                if ((white && board.Castling.WhiteQueenside) ||
                    (!white && board.Castling.BlackQueenside))
                {
                    bool rookPresent = squares[homeRank, 0] == (white ? Piece.WR : Piece.BR);

                    if (rookPresent && squares[homeRank, 1] == Piece.Empty &&
                        squares[homeRank, 2] == Piece.Empty &&
                        squares[homeRank, 3] == Piece.Empty &&
                        !IsSquareAttacked(board, homeRank, 2, !white) &&
                        !IsSquareAttacked(board, homeRank, 3, !white) &&
                        !IsSquareAttacked(board, homeRank, 4, !white))
                    {
                        moves.Add(new Move(4, homeRank, 2, homeRank));
                    }
                }
            }

            return moves;
        }
        private bool IsWhite(Piece piece)
        {
            return piece >= Piece.WP && piece <= Piece.WK;
        }

        private bool IsFriendlyPiece(Piece piece, bool whiteToMove)
        {
            return (piece != Piece.Empty) && (IsWhite(piece) == whiteToMove);
        }

        private bool IsInBounds(int rank, int file)
        {
            return rank >= 0 && rank < 8 && file >= 0 && file < 8;
        }
        public bool IsSquareAttacked(Board board, int targetRank, int targetFile, bool byWhite)
        {
            // 1. Check Pawns (fastest path - most common attackers)
            int pawnDir = byWhite ? 1 : -1;
            if (targetRank + pawnDir < 8 && targetRank + pawnDir >= 0)
            {
                if (targetFile > 0 && board.squares[targetRank + pawnDir, targetFile - 1] == (byWhite ? Piece.WP : Piece.BP))
                    return true;
                if (targetFile < 7 && board.squares[targetRank + pawnDir, targetFile + 1] == (byWhite ? Piece.WP : Piece.BP))
                    return true;
            }

            // 2. Check Knights (8 possible positions)
            ReadOnlySpan<(int, int)> knightOffsets = stackalloc (int, int)[]
            {
        (-2, -1), (-2, 1), (-1, -2), (-1, 2),
        (1, -2), (1, 2), (2, -1), (2, 1)
    };

            foreach (var (dr, df) in knightOffsets)
            {
                int r = targetRank + dr;
                int f = targetFile + df;
                if (r >= 0 && r < 8 && f >= 0 && f < 8 &&
                    board.squares[r, f] == (byWhite ? Piece.WN : Piece.BN))
                    return true;
            }

            // 3. Check Sliding Pieces (Queens/Rooks/Bishops)
            ReadOnlySpan<(int, int)> directions = stackalloc (int, int)[]
            {
        (-1, 0), (1, 0), (0, -1), (0, 1),    // Rooks
        (-1, -1), (-1, 1), (1, -1), (1, 1)    // Bishops
    };

            for (int i = 0; i < directions.Length; i++)
            {
                var (dr, df) = directions[i];
                int r = targetRank + dr;
                int f = targetFile + df;

                while (r >= 0 && r < 8 && f >= 0 && f < 8)
                {
                    Piece piece = board.squares[r, f];
                    if (piece != Piece.Empty)
                    {
                        if (IsWhite(piece) == byWhite)
                        {
                            bool isDiagonal = (i >= 4); // Last 4 directions are diagonal
                            if (isDiagonal)
                            {
                                if (piece == (byWhite ? Piece.WB : Piece.BB) ||
                                    piece == (byWhite ? Piece.WQ : Piece.BQ))
                                    return true;
                            }
                            else // Orthogonal
                            {
                                if (piece == (byWhite ? Piece.WR : Piece.BR) ||
                                    piece == (byWhite ? Piece.WQ : Piece.BQ))
                                    return true;
                            }
                        }
                        break; // Blocked by any piece
                    }
                    r += dr;
                    f += df;
                }
            }

            // 4. Check King (adjacent squares)
            for (int dr = -1; dr <= 1; dr++)
            {
                for (int df = -1; df <= 1; df++)
                {
                    if (dr == 0 && df == 0) continue;
                    int r = targetRank + dr;
                    int f = targetFile + df;
                    if (r >= 0 && r < 8 && f >= 0 && f < 8 &&
                        board.squares[r, f] == (byWhite ? Piece.WK : Piece.BK))
                        return true;
                }
            }

            return false;
        }
    }
}

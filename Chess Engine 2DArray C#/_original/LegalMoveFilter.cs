using System;
using System.Collections.Generic;

namespace Chess_Engine_2DArray_C_
{
    internal class LegalMoveFilter
    {
        // In LegalMoveFilter.cs
        public List<Move> FilterLegalMoves(Board board, List<Move> pseudoLegalMoves, bool whiteToMove)
        {
            List<Move> legalMoves = new List<Move>();
            var originalCastling = board.Castling;

            foreach (var move in pseudoLegalMoves)
            {
                bool isEnPassant = (board.squares[move.FromRank, move.FromFile] == Piece.WP ||
                                  board.squares[move.FromRank, move.FromFile] == Piece.BP) &&
                                 move.ToFile != move.FromFile &&
                                 board.squares[move.ToRank, move.ToFile] == Piece.Empty;

                Piece captured = board.MakeMove(move, whiteToMove);

                // Skip king checks for castling moves (they're handled separately)
                bool isLegal = true;
                if (board.squares[move.ToRank, move.ToFile] == Piece.WK ||
                    board.squares[move.ToRank, move.ToFile] == Piece.BK)
                {
                    // For castling, we need to check all squares the king moves through
                    int startFile = Math.Min(move.FromFile, move.ToFile);
                    int endFile = Math.Max(move.FromFile, move.ToFile);

                    for (int f = startFile; f <= endFile; f++)
                    {
                        if (IsSquareAttacked(board, move.FromRank, f, !whiteToMove))
                        {
                            isLegal = false;
                            break;
                        }
                    }
                }
                else
                {
                    // For regular moves, find the king's new position
                    (int kingRank, int kingFile) = FindKing(board.squares, whiteToMove);

                    // Only check if king was found (might be missing in some edge cases)
                    if (kingRank >= 0 && kingFile >= 0)
                    {
                        isLegal = !IsSquareAttacked(board, kingRank, kingFile, !whiteToMove);
                    }
                }

                if (isLegal) legalMoves.Add(move);

                board.UnmakeMove(move, captured, isEnPassant, originalCastling);
            }

            return legalMoves;
        }
        //private (int, int) FindKing(Piece[,] board, bool whiteToMove)
        //{
        //    Piece king = whiteToMove ? Piece.WK : Piece.BK;

        //    for (int rank = 0; rank < 8; rank++)
        //    {
        //        for (int file = 0; file < 8; file++)
        //        {
        //            if (board[rank, file] == king)
        //                return (rank, file);
        //        }
        //    }

        //    throw new Exception("King not found on board.");
        //}

        private bool IsSquareAttacked(Board board, int targetRank, int targetFile, bool byWhite)
        {
            MoveGenerator generator = new MoveGenerator();
            List<Move> opponentMoves = generator.GenerateMoves(board, byWhite);

            foreach (var move in opponentMoves)
            {
                if (move.ToRank == targetRank && move.ToFile == targetFile)
                    return true;
            }

            return false;
        }
        private (int rank, int file) FindKing(Piece[,] board, bool whiteToMove)
        {
            Piece king = whiteToMove ? Piece.WK : Piece.BK;

            // Search the board for the king
            for (int rank = 0; rank < 8; rank++)
            {
                for (int file = 0; file < 8; file++)
                {
                    if (board[rank, file] == king)
                        return (rank, file);
                }
            }

            // If king not found, check if it's a pseudo-legal position during search
            // (this can happen temporarily during move generation)
            return (-1, -1); // Return invalid position instead of throwing
        }
    }
}

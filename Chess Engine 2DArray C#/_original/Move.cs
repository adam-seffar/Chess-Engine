using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess_Engine_2DArray_C_
{
    public class Move
    {
        public int FromRank, FromFile;
        public int ToRank, ToFile;
        public Piece? PromotionPiece; // Nullable to indicate non-promotion moves


        public Move(int fromFile, int fromRank, int toFile, int toRank, Piece? promotionPiece = null)
        {
            FromFile = fromFile;
            FromRank = fromRank;
            ToFile = toFile;
            ToRank = toRank;
            PromotionPiece = promotionPiece;
        }

        public override string ToString()
        {
            string moveStr = $"{(char)('a' + FromFile)}{8 - FromRank}{(char)('a' + ToFile)}{8 - ToRank}";
            if (PromotionPiece.HasValue)
            {
                moveStr += $"={PromotionPiece.Value.ToString().TrimStart('W', 'B')}";
            }
            return moveStr;
        }
    }
 }

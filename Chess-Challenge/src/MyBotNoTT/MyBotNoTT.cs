using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

public class MyBotNoTT : IChessBot
{
    private int nodes;
    private int qNodes;

    private int currentSearchDepth;
    private int maxSearchTimeAllowed;

    private List<Move> bestMovesFound = new();
    private int bestMoveScore;

    List<Move> bestMovesFoundThisItr = new();
    int bestScoreThisItr = -lVal;

    private const int lVal = 100000;

    private Board board;
    private Timer timer;

    private Random rng;


    public MyBotNoTT()
    {
        Logger.Init();
        rng = new Random();
    }

    private void Reset()
    {
        nodes = 0;
        qNodes = 0;
        currentSearchDepth = 1;
        bestMovesFound.Clear();
        bestMoveScore = -lVal;
    }

    private void UpdateAllowedTime()
    {
        var div = 30;
        var left = timer.MillisecondsRemaining - 5000;
        if (left < 30000) div = 15;
        if (left < 15000) div = 10;

        maxSearchTimeAllowed = Math.Max(50, left / div);
        Console.WriteLine($"Going to spend {maxSearchTimeAllowed}ms thinking");
    }

    public Move Think(Board in_board, Timer in_timer)
    {
        if (in_board.PlyCount == 0) return new Move("e2e4", in_board);
        if (in_board.PlyCount == 1) return new Move("d7d5", in_board);

        board = in_board;
        timer = in_timer;

        if (!board.IsInCheck())
        {
            int evalForUs = Evaluate();
            board.TrySkipTurn();
            int evalForThem = Evaluate();
            board.UndoSkipTurn();
            Console.WriteLine($"Before we make a move, our eval is: {evalForUs}, their eval is: {evalForThem}");
        }


        Reset();
        UpdateAllowedTime();
        FindBestIter();


        return Best();
    }

    private Move Best()
    {
        if (bestMovesFound.Count == 0)
        {
            Console.WriteLine($"No good moves found?");
            if (bestMovesFoundThisItr.Count > 0) return bestMovesFoundThisItr[0];

            var validMoves = board.GetLegalMoves();
            return validMoves[rng.Next(validMoves.Length)];
        }

        var options = bestMovesFound.Count;
        var chosen = bestMovesFound[rng.Next(options)];
        Console.WriteLine($"Search over at depth {currentSearchDepth - 1}. Best eval: {bestMoveScore}, move options: {options}, playing: {chosen}. Spent: {timer.MillisecondsElapsedThisTurn}");

        return chosen;
    }

    private void FindBestIter()
    {
        var moves = GetSortedMoves();
        while (true)
        {
            if (timer.MillisecondsElapsedThisTurn > maxSearchTimeAllowed) return;

            float minPrec = maxSearchTimeAllowed * 0.4f;
            if (timer.MillisecondsElapsedThisTurn > minPrec)
            {
                Console.WriteLine($"Less than 40% time remaining, not starting next search. Needed {minPrec} to continue");
                return;
            }

            bestMovesFoundThisItr = new();
            bestScoreThisItr = -lVal;

            foreach (var move in moves)
            {
                board.MakeMove(move);
                var moveScore = -Negamax(currentSearchDepth);
                board.UndoMove(move);

                // If we exited negamax due to time, we need to discard current itr
                if (timer.MillisecondsElapsedThisTurn > maxSearchTimeAllowed) return;

                if (moveScore > bestScoreThisItr)
                {
                    bestScoreThisItr = moveScore;
                    bestMovesFoundThisItr.Clear();
                    bestMovesFoundThisItr.Add(move);
                }
                else if (moveScore == bestScoreThisItr)
                {
                    bestMovesFoundThisItr.Add(move);
                }
            }

            bestMoveScore = bestScoreThisItr;
            bestMovesFound = bestMovesFoundThisItr;
            Console.WriteLine($"At depth {currentSearchDepth} found best move with score {bestMoveScore} ({bestMovesFound.Count} options) (Nodes: {nodes}, QNodes: {qNodes}) Time: {timer.MillisecondsElapsedThisTurn}ms");

            currentSearchDepth++;
        }
    }

    private int Negamax(int depth, int ply = 1, int alpha = -lVal, int beta = lVal)
    {
        if (timer.MillisecondsElapsedThisTurn > maxSearchTimeAllowed) return 0;
        nodes++;

        if (board.IsInCheckmate()) return -lVal + ply;
        if (board.IsDraw()) return 0;

        if (depth == 0)
        {
            return QSearch(alpha, beta);// Evaluate(board);
        }

        int startAlpha = alpha;

        var moves = GetSortedMoves();
        var bestMove = Move.NullMove;
        int bestEval = -lVal;

        foreach (var move in moves)
        {
            board.MakeMove(move);
            int moveScore = -Negamax(depth - 1, ply + 1, -beta, -alpha);
            board.UndoMove(move);

            if (moveScore > bestEval)
            {
                bestEval = moveScore;
                bestMove = move;
                alpha = Math.Max(bestEval, alpha);
                if (alpha >= beta) break;
            }
        }


        return bestEval;
    }

    private int QSearch(int alpha, int beta)
    {
        qNodes++;
        int pat = Evaluate();
        if (pat >= beta) return beta;

        if (alpha < pat) alpha = pat;

        var captures = GetSortedMoves(true);
        // captures.
        foreach (var capture in captures)
        {
            board.MakeMove(capture);
            int score = -QSearch(-beta, -alpha);
            board.UndoMove(capture);

            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }

        return alpha;
    }

    // Quick sorting system that works without full array sorts

    private Move[] GetSortedMoves(bool onlyCapture = false)
    {
        var moves = board.GetLegalMoves(onlyCapture);
        var evals = moves.Select(m => EvalMove(m));
        Array.Sort(evals.ToArray(), moves);
        Array.Reverse(moves);
        return moves;
    }

    private int EvalMove(Move move)
    {
        int eval = 0;
        if (move.IsCastles) eval += 10;
        if (move.IsPromotion) eval += 50;
        if (move.IsCapture) eval += 10 * (int)move.CapturePieceType - (int)move.MovePieceType;
        if (board.SquareIsAttackedByOpponent(move.TargetSquare)) eval -= 5;

        return eval;
    }

    private int BoolToMult(bool color)
    {
        return color ? 1 : -1;
    }

    public int Evaluate()
    {
        var mobilityMult = 1;
        var eval = CalculateMaterial() * BoolToMult(board.IsWhiteToMove);
        if (board.IsInCheck()) eval -= 50; // Check is bad thing for the person who's current move it is
        var mobility = CalculateMobility();
        eval += mobility * mobilityMult;
        return eval;
    }

    private int CalculateMobility()
    {
        var moves = board.GetLegalMoves();
        return moves.Length;
    }

    private int PieceWorth(PieceType type)
    {
        int[] scores = { 0, 100, 320, 330, 500, 900, 10000 };
        return scores[(int)type];
    }

    static int PiecePosScore(Piece piece, bool kingEnd = false)
    {
        ulong[] values = { 231520005970560296, 371413138147804744, 376235690637563454, 587905079315048590, 448258289281955402, 876384074027600299, 448259453218158153, 948441732506814890, 452761880320570952, 952945332135234216, 457549218373334601, 889894855731940006, 340172791585601081, 614891522810808458, 344109996662162728, 326394734067484232 };
        int file = piece.Square.File;
        int rank = piece.Square.Rank;
        if (file > 3) file = 7 - file;
        if (piece.IsWhite) rank = 7 - rank;

        var index = file + rank * 4;
        var offset = ((int)piece.PieceType - 1) * 4 + (index % 2 * 32) + (kingEnd && piece.PieceType == PieceType.King ? 4 : 0);
        // Console.WriteLine($"Rank: {rank}, File: {file}, Index: {index}   Type Offset: {((int)type - 1) * 4}  Index Sub Offset: {(index % 2 * 32)} King End Offset: {(kingEnd ? 4 : 0)}  --  {values[index / 2] & (mask << offset)}");
        int result = (int)((values[index / 2] >> offset) & 0b1111);
        return (result - 8) * 5;
    }

    // Returns the material of a board where the total is zero for equal, + for white, and - for black
    public int CalculateMaterial()
    {
        int eval = 0;
        var lists = board.GetAllPieceLists();

        foreach (var list in lists)
        {
            eval += PieceWorth(list.TypeOfPieceInList) * list.Count * BoolToMult(list.IsWhitePieceList);
            // foreach (var piece in list) eval += PiecePosScore(piece, board.PlyCount > 48) * BoolToMult(list.IsWhitePieceList);
        }

        return eval;
    }
}
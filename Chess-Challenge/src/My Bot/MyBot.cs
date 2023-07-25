using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    private bool me;
    private int checks = 0;
    private Random rng;
    private const int defaultSearchDepth = 5;
    private float lastTimeSpent = 0;
    private int lastDepth = 3;
    public MyBot()
    {
        Logger.Init();
        rng = new Random(1776);
    }

    public Move Think(Board board, Timer timer)
    {
        checks = 0;
        me = board.IsWhiteToMove;

        Move[] moves = board.GetLegalMoves();
        Move best = Move.NullMove;
        List<Move> equalToBest = new List<Move>();
        var bestScore = -100000;

        int searchDepth = lastDepth;
        int maxTimePerMove = timer.MillisecondsRemaining > 30000 ? 2000 : 1000;
        int minTimePerMove = timer.MillisecondsRemaining > 30000 ? 500 : 250;
        if (lastTimeSpent > maxTimePerMove) searchDepth--;
        if (lastTimeSpent < minTimePerMove) searchDepth++;

        foreach (var move in moves)
        {
            board.MakeMove(move);

            var score = MinimaxEval(board, searchDepth); // If we are black, invert score. Black is winning when negative
            var resultScore = score * Mult(me);
            if (resultScore > bestScore)
            {
                bestScore = resultScore;
                best = move;
                equalToBest.Clear();
            }
            else if (resultScore == bestScore)
            {
                equalToBest.Add(move);
            }

            board.UndoMove(move);
        }

        if (equalToBest.Count > 0)
        {
            best = equalToBest[rng.Next(equalToBest.Count)];
        }

        if (best == Move.NullMove) best = moves[rng.Next(moves.Length)];


        lastTimeSpent = timer.MillisecondsElapsedThisTurn;
        lastDepth = searchDepth;
        Console.WriteLine($"Eval: {bestScore}. Number of node searched: {checks}. Used depth: {searchDepth}. Spent: {lastTimeSpent}ms");
        return best;
    }

    private int MinimaxEval(Board board, int depth = defaultSearchDepth, int alpha = int.MinValue, int beta = int.MaxValue)
    {
        checks++;
        if (board.IsInCheckmate()) return -100000 * Mult(board.IsWhiteToMove); // Checkmate and white to move = black won
        if (board.IsDraw()) return 0;

        if (depth == 0)
        {
            return Evaluate(board);
        }

        var moves = GetSortedMoves(board);
        foreach (var move in moves)
        {
            board.MakeMove(move);
            int moveScore = MinimaxEval(board, depth - 1, alpha, beta);
            board.UndoMove(move);

            if (board.IsWhiteToMove) alpha = Math.Max(alpha, moveScore);
            else beta = Math.Min(beta, moveScore);

            if (beta <= alpha) break;
        }

        if (board.IsWhiteToMove) return alpha; // White wants highest number
        else return beta; // Black wants least
    }

    private Move[] GetSortedMoves(Board board)
    {
        var moves = board.GetLegalMoves();
        List<List<Move>> sMoves = new() { new(), new(), new() };
        foreach (var move in moves)
        {
            if (move.IsCapture) sMoves[0].Add(move);
            else if (board.SquareIsAttackedByOpponent(move.TargetSquare)) sMoves[2].Add(move);
            else sMoves[1].Add(move);
        }

        return sMoves[0].Concat(sMoves[1]).Concat(sMoves[2]).ToArray();
    }

    private int Mult(bool color)
    {
        return color ? 1 : -1;
    }

    public int Evaluate(Board board)
    {
        var eval = CalculateMaterial(board);
        if (board.IsInCheck()) eval -= 100 * Mult(board.IsWhiteToMove); // White to move && check = white is in check, so subtract from eval

        return eval;
    }

    // public int CalculateBoardCoverage(Board board)
    // {
    //     // board.GetKingSquare
    //     for (int i = 0; i < 64; i++)
    //     {
    //         var square = new Square(i);
    //         // board.SquareIsAttackedByOpponent(square);
    //         
    //     }
    // }

    private int PieceWorth(PieceType type)
    {
        int[] scores = { 0, 100, 300, 350, 500, 900, 10000 };
        return scores[(int)type];
    }

    public int CalculateMaterial(Board board)
    {
        int eval = 0;
        foreach (var list in board.GetAllPieceLists())
        {
            eval += PieceWorth(list.TypeOfPieceInList) * list.Count * (list.IsWhitePieceList == true ? 1 : -1);
        }

        return eval;
    }
}
using ChessChallenge.API;
using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
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

    private const byte TT_INVALID = 0, TT_EXACT = 1, TT_LOWERBOUND = 2, TT_UPPERBOUND = 3;
    struct Transposition
    {
        public ulong zorbristKey;
        public Move move;
        public int evaluation;
        public sbyte depth;
        public byte flag;
    };

    Transposition[] ttable;
    ulong ttableMask = 0x7FFFFF;

    // Tyrant's Pesto implementation:
    readonly int[] phaseMults = { 0, 1, 1, 2, 4, 0 };
    private short[] pieceValueMap = { 100, 310, 320, 500, 1001, 20000, 110, 300, 310, 500, 1001, 20000 };
    private decimal[] compressedPesto = {
        63746705523041458768562654720m, 71818693703096985528394040064m, 75532537544690978830456252672m, 75536154932036771593352371712m, 76774085526445040292133284352m, 3110608541636285947269332480m, 936945638387574698250991104m, 75531285965747665584902616832m,
        77047302762000299964198997571m, 3730792265775293618620982364m, 3121489077029470166123295018m, 3747712412930601838683035969m, 3763381335243474116535455791m, 8067176012614548496052660822m, 4977175895537975520060507415m, 2475894077091727551177487608m,
        2458978764687427073924784380m, 3718684080556872886692423941m, 4959037324412353051075877138m, 3135972447545098299460234261m, 4371494653131335197311645996m, 9624249097030609585804826662m, 9301461106541282841985626641m, 2793818196182115168911564530m,
        77683174186957799541255830262m, 4660418590176711545920359433m, 4971145620211324499469864196m, 5608211711321183125202150414m, 5617883191736004891949734160m, 7150801075091790966455611144m, 5619082524459738931006868492m, 649197923531967450704711664m,
        75809334407291469990832437230m, 78322691297526401047122740223m, 4348529951871323093202439165m, 4990460191572192980035045640m, 5597312470813537077508379404m, 4980755617409140165251173636m, 1890741055734852330174483975m, 76772801025035254361275759599m,
        75502243563200070682362835182m, 78896921543467230670583692029m, 2489164206166677455700101373m, 4338830174078735659125311481m, 4960199192571758553533648130m, 3420013420025511569771334658m, 1557077491473974933188251927m, 77376040767919248347203368440m,
        73949978050619586491881614568m, 77043619187199676893167803647m, 1212557245150259869494540530m, 3081561358716686153294085872m, 3392217589357453836837847030m, 1219782446916489227407330320m, 78580145051212187267589731866m, 75798434925965430405537592305m,
        68369566912511282590874449920m, 72396532057599326246617936384m, 75186737388538008131054524416m, 77027917484951889231108827392m, 73655004947793353634062267392m, 76417372019396591550492896512m, 74568981255592060493492515584m, 70529879645288096380279255040m,
    };

    private int[][] pieceTable;

    public MyBot()
    {
        rng = new Random(1776);
        ttable = new Transposition[ttableMask + 1];

        pieceTable = new int[64][];
        for (int i = 0; i < 64; i++)
        {
            int pieceType = 0;
            pieceTable[i] = decimal.GetBits(compressedPesto[i]).Take(3).SelectMany(c => BitConverter.GetBytes(c).Select((byte square) => (int)((sbyte)square * 1.461) + pieceValueMap[pieceType++])).ToArray();
        }
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
        bool earlyExit = false;
        while (true)
        {
            if (timer.MillisecondsElapsedThisTurn > maxSearchTimeAllowed) return;

            // float minPrec = maxSearchTimeAllowed * 0.4f;
            // if (timer.MillisecondsElapsedThisTurn > minPrec)
            // {
            //     Console.WriteLine($"Less than 40% time remaining, not starting next search. Needed {minPrec} to continue");
            //     return;
            // }

            bestMovesFoundThisItr = new();
            bestScoreThisItr = -lVal;

            foreach (var move in moves)
            {
                board.MakeMove(move);
                var moveScore = -Negamax(currentSearchDepth);
                board.UndoMove(move);

                // If we exited negamax due to time, break out.
                if (timer.MillisecondsElapsedThisTurn > maxSearchTimeAllowed) { earlyExit = true; break; }

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

            if (!earlyExit || bestScoreThisItr > bestMoveScore)
            {
                bestMoveScore = bestScoreThisItr;
                bestMovesFound = bestMovesFoundThisItr;
                if (earlyExit) Console.WriteLine($"Found better move with partial itteration!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Console.WriteLine($"At depth {currentSearchDepth} found best move with score {bestMoveScore} ({bestMovesFound.Count} options) (Nodes: {nodes}, QNodes: {qNodes}) Time: {timer.MillisecondsElapsedThisTurn}ms");
            }
            else
            {
                Console.WriteLine($"Ended early in {currentSearchDepth}, was unable to find better in allowed time. (Nodes: {nodes}, QNodes: {qNodes}) Time: {timer.MillisecondsElapsedThisTurn}ms");
            }

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
            return QSearch(alpha, beta);
        }

        int startAlpha = alpha;

        ref Transposition transposition = ref ttable[board.ZobristKey & ttableMask];
        if (transposition.zorbristKey == board.ZobristKey && transposition.flag != TT_INVALID && transposition.depth >= depth)
        {
            int eval = transposition.evaluation;
            if (transposition.flag == TT_EXACT) return eval;
            else if (transposition.flag == TT_LOWERBOUND && eval >= beta) return eval;// alpha = Math.Max(alpha, transposition.evaluation);
            else if (transposition.flag == TT_UPPERBOUND && eval <= alpha) return eval;// beta = Math.Min(beta, transposition.evaluation);
            // if (alpha >= beta) return transposition.evaluation;
        }

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


        transposition.evaluation = bestEval;
        transposition.zorbristKey = board.ZobristKey;
        transposition.move = bestMove;
        if (bestEval <= startAlpha) transposition.flag = TT_UPPERBOUND;
        else if (bestEval >= beta) transposition.flag = TT_LOWERBOUND;
        else transposition.flag = TT_EXACT;
        transposition.depth = (sbyte)depth;

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


    private Move[] GetSortedMoves(bool onlyCapture = false)
    {
        var tEntry = ttable[board.ZobristKey & ttableMask];
        var moves = board.GetLegalMoves(onlyCapture);
        var evals = moves.Select(m => EvalMove(m, tEntry));
        Array.Sort(evals.ToArray(), moves);
        Array.Reverse(moves);
        return moves;
    }

    private int EvalMove(Move move, Transposition tEntry)
    {
        int eval = 0;
        if (tEntry.zorbristKey == board.ZobristKey && move == tEntry.move) return 1000;
        if (move.IsCastles) eval += 10;
        if (move.IsPromotion) eval += 50;
        if (move.IsCapture) eval += 10 * (int)move.CapturePieceType - (int)move.MovePieceType;
        if (board.SquareIsAttackedByOpponent(move.TargetSquare)) eval -= 5;

        return eval;
    }

    private int BoolToMult(bool color) => color ? 1 : -1;

    private int EvalMaterialAndPST()
    {
        int midgame = 0, endgame = 0, phase = 0;
        foreach (var list in board.GetAllPieceLists())
        {
            foreach (var piece in list)
            {
                var squareIndex = piece.Square.Index ^ (list.IsWhitePieceList ? 56 : 0);
                var pieceType = (int)list.TypeOfPieceInList - 1;
                var mult = BoolToMult(list.IsWhitePieceList);
                midgame += pieceTable[squareIndex][pieceType] * mult;
                endgame += pieceTable[squareIndex][pieceType + 6] * mult;
                phase += phaseMults[pieceType];
            }
        }
        phase = Math.Min(phase, 24);
        return (midgame * phase + endgame * (24 - phase)) / 24 * BoolToMult(board.IsWhiteToMove);
    }


    public int Evaluate()
    {
        var eval = EvalMaterialAndPST();
        if (board.IsInCheck()) eval -= 50; // Check is bad thing for the person who's current move it is
        eval += board.GetLegalMoves().Length;
        return eval;
    }
}
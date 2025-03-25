using EzBot.Models;

namespace EzBot.Core.Optimization
{
    public class BacktestAccount(double initialBalance, double feePercentage, int leverage = 10, double maxDrawdownPercent = 30)
    {
        private readonly double _initialBalance = initialBalance;
        private readonly double _feePercentage = feePercentage / 100.0;
        private readonly int _leverage = leverage;
        private readonly List<BacktestTrade> _completedTrades = [];
        private readonly Dictionary<int, BacktestTrade> _activeTrades = [];
        private readonly double _maxDrawdownPercent = maxDrawdownPercent;

        private double _currentBalance = initialBalance;
        private int _nextTradeId = 1;
        private const double _riskPercentage = 0.01; // 1% risk per trade

        public bool IsLiquidated { get; set; } = false;
        public double StartUnixTime { get; set; } = 0;
        public double EndUnixTime { get; set; } = 0;

        public double CurrentBalance => _currentBalance;

        public int OpenPosition(TradeType type, double price, double stopLoss, int barIndex)
        {
            // Console.WriteLine($"Opening {type} position at {price} with stop loss at {stopLoss}");
            // Calculate position size based on risk management and leverage
            double riskAmount = _currentBalance * _riskPercentage; // 1% of current balance
            double priceDifference = Math.Abs(price - stopLoss);
            // Multiply by leverage to increase position size
            double positionSize = priceDifference > 0 ? riskAmount / priceDifference * _leverage : 0;

            var trade = new BacktestTrade
            {
                Type = type,
                EntryPrice = price,
                EntryBar = barIndex,
                StopLoss = stopLoss,
                PositionSize = positionSize
            };

            // Calculate margin required (position value / leverage)
            double marginUsed = price * positionSize / _leverage;

            // Reduce balance by both margin used and trading fee
            _currentBalance -= marginUsed;

            // Store the trade with its ID
            int tradeId = _nextTradeId++;
            _activeTrades[tradeId] = trade;

            return tradeId;
        }

        public void ClosePosition(int tradeId, double price, int barIndex)
        {
            if (!_activeTrades.TryGetValue(tradeId, out var trade))
                return;

            trade.ExitPrice = price;
            trade.ExitBar = barIndex;

            // Calculate profit or loss based on position size
            double priceDifference;
            if (trade.Type == TradeType.Long)
                priceDifference = trade.ExitPrice - trade.EntryPrice;
            else
                priceDifference = trade.EntryPrice - trade.ExitPrice;

            // Calculate total P&L using position size
            double pnl = priceDifference * trade.PositionSize;

            // Apply trading fee on exit
            double tradingFee = price * trade.PositionSize * _feePercentage;
            pnl -= tradingFee;

            trade.Profit = pnl;
            _currentBalance += pnl;
            _completedTrades.Add(trade);

            // Return the margin that was locked
            double marginUsed = trade.EntryPrice * trade.PositionSize / _leverage;
            _currentBalance += marginUsed;

            // Remove from active trades
            _activeTrades.Remove(tradeId);

            // Account is liquidated if balance drops below 5% of initial balance or drawdown exceeds max allowed
            if (_currentBalance <= _initialBalance * 0.05 || CalculateMaxDrawdown() >= _maxDrawdownPercent)
            {
                IsLiquidated = true;
            }
        }

        public int CalculateDuration()
        {
            // create dattime objects from unix timestamps
            var startDate = DateTimeOffset.FromUnixTimeSeconds((long)StartUnixTime).DateTime;
            var endDate = DateTimeOffset.FromUnixTimeSeconds((long)EndUnixTime).DateTime;

            // Calculate the duration and ensure it's at least 1 day if there's any time difference
            double totalDays = (endDate - startDate).TotalDays;

            // Round up to nearest day and ensure minimum of 1 day when there's any activity
            return totalDays > 0
                ? (int)Math.Ceiling(totalDays)
                : (endDate > startDate ? 1 : 0);
        }

        public void LiquidateAccount()
        {
            IsLiquidated = true;
        }

        public BacktestResult GenerateResult()
        {
            // Close any remaining positions at their last known value
            // (This would be typically handled by the BacktestRunner)

            var winningTrades = _completedTrades.Count(t => t.Profit > 0);
            var losingTrades = _completedTrades.Count(t => t.Profit <= 0);

            double totalProfit = _completedTrades.Where(t => t.Profit > 0).Sum(t => t.Profit);
            double totalLoss = Math.Abs(_completedTrades.Where(t => t.Profit <= 0).Sum(t => t.Profit));

            // Calculate profit factor, avoid division by zero
            double profitFactor = totalLoss == 0 ? (totalProfit > 0 ? double.MaxValue : 0) : totalProfit / totalLoss;

            return new BacktestResult
            {
                InitialBalance = _initialBalance,
                FinalBalance = _currentBalance,
                TotalTrades = _completedTrades.Count,
                WinningTrades = winningTrades,
                LosingTrades = losingTrades,
                ProfitFactor = profitFactor,
                MaxDrawdown = CalculateMaxDrawdown(),
                SharpeRatio = CalculateSharpeRatio(),
                Trades = _completedTrades,
                BacktestDurationDays = CalculateDuration(),
                StartDate = DateTimeOffset.FromUnixTimeSeconds((long)StartUnixTime).DateTime,
                EndDate = DateTimeOffset.FromUnixTimeSeconds((long)EndUnixTime).DateTime,
                IsValidResult = !IsLiquidated
            };
        }

        private double CalculateMaxDrawdown()
        {
            double peak = _initialBalance;
            double maxDrawdown = 0;
            double runningBalance = _initialBalance;

            foreach (var trade in _completedTrades)
            {
                runningBalance += trade.Profit;
                peak = Math.Max(peak, runningBalance);
                double drawdown = (peak - runningBalance) / peak * 100;
                maxDrawdown = Math.Max(maxDrawdown, drawdown);
            }

            return maxDrawdown;
        }

        private double CalculateSharpeRatio()
        {
            if (_completedTrades.Count == 0) return 0;

            double mean = _completedTrades.Average(t => t.Profit);
            double stdDev = Math.Sqrt(_completedTrades.Sum(t => Math.Pow(t.Profit - mean, 2)) / _completedTrades.Count);

            return stdDev == 0 ? 0 : mean / stdDev;
        }

        public Dictionary<int, BacktestTrade> GetActiveTrades()
        {
            return _activeTrades;
        }

        public BacktestTrade? GetTradeById(int tradeId)
        {
            return _activeTrades.TryGetValue(tradeId, out var trade) ? trade : null;
        }
    }
}
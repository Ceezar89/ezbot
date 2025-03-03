namespace EzBot.Models;

public class OptimizationResult
{
    public required IndicatorParameterDto[] BestParameters { get; set; }
    public required BacktestResult BacktestResult { get; set; }
    public required List<BacktestResult> AllResults { get; set; }
    public int TotalCombinationsTested { get; set; }
    public TimeFrame TimeFrame { get; set; } = TimeFrame.Minute1;
}

public class IndicatorParameterDto
{
    public required string IndicatorType { get; set; }
    public required Dictionary<string, object> Parameters { get; set; }
}
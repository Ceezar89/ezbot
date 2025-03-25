namespace EzBot.Models;

public class OptimizationResult
{
    public required IndicatorParameterDto[] BestParameters { get; set; }
    public required BacktestResult BacktestResult { get; set; }
}

public class IndicatorParameterDto
{
    public required string IndicatorType { get; set; }
    public required Dictionary<string, object> Parameters { get; set; }
}
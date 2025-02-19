namespace EzBot.Core.IndicatorParameter;

public interface IIndicatorParameter : IEquatable<IIndicatorParameter>
{
    public string Name { get; set; }
    void IncrementSingle();
    bool CanIncrement();
}

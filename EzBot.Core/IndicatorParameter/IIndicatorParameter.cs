namespace EzBot.Core.IndicatorParameter;

public interface IIndicatorParameter : IEquatable<IIndicatorParameter>
{
    string Name { get; set; }
    void IncrementSingle();
    bool CanIncrement();
}

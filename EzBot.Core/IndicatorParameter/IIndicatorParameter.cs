namespace EzBot.Core.IndicatorParameter;

public interface IIndicatorParameter : IEquatable<IIndicatorParameter>
{
    string Name { get; set; }
    void IncrementSingle();
    bool CanIncrement();
    List<ParameterDescriptor> GetProperties();
    void UpdateFromDescriptor(ParameterDescriptor descriptor);
    IIndicatorParameter DeepClone();
    IIndicatorParameter GetRandomNeighbor(Random random);
    int GetPermutationCount();
    void Reset();
    byte[] ToBinary();
    IIndicatorParameter FromBinary(byte[] data);
}

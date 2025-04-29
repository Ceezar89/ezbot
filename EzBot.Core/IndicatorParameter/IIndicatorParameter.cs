namespace EzBot.Core.IndicatorParameter;

public interface IIndicatorParameter : IEquatable<IIndicatorParameter>
{
    string Name { get; }
    bool IncrementSingle();
    List<ParameterDescriptor> GetProperties();
    void UpdateFromDescriptor(ParameterDescriptor descriptor);
    IIndicatorParameter DeepClone();
    IIndicatorParameter GetRandomNeighbor(Random random);
    int GetPermutationCount();
    void Reset();
    byte[] ToBinary();
    void UpdateFromBinary(byte[] data);
    List<byte[]> GenerateAllPermutationsBinary();
}

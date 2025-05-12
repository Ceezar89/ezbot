using EzBot.Models;

namespace EzBot.Core.IndicatorParameter;

public class EtmaParameter : IndicatorParameterBase
{
    private int _length = 14;
    private SignalStrength _signalStrength = SignalStrength.VeryStrong;

    // Ranges
    private static readonly (int Min, int Max) LengthRange = (10, 100);
    private static readonly (int Min, int Max) SignalStrengthRange = (0, 2);

    // Steps
    private const int LengthRangeStep = 10;
    private const int SignalStrengthRangeStep = 1;

    // Correctly calculate the number of steps for each parameter range
    private static readonly int LengthPermutations = CalculateSteps(LengthRange.Min, LengthRange.Max, LengthRangeStep);
    private static readonly int SignalStrengthPermutations = CalculateSteps(SignalStrengthRange.Min, SignalStrengthRange.Max, SignalStrengthRangeStep);

    public static readonly int TotalPermutations = LengthPermutations * SignalStrengthPermutations;

    // Type identifier for EtmaParameter (unique across all parameter types)
    private const byte TYPE_ID = 0x03;

    protected override byte GetTypeIdentifier() => TYPE_ID;

    protected override byte[] GetParameterSpecificBinaryData()
    {
        byte[] data = new byte[8]; // 4 bytes for Length, 4 bytes for SignalStrength

        // Store Length (4 bytes)
        BitConverter.GetBytes(_length).CopyTo(data, 0);

        // Store SignalStrength (4 bytes)
        BitConverter.GetBytes((int)_signalStrength).CopyTo(data, 4);

        return data;
    }

    /// <summary>
    /// Updates the instance fields from the parameter-specific binary data.
    /// </summary>
    /// <param name="data">Byte array containing only the specific data for this parameter type.</param>
    /// <exception cref="ArgumentException">Thrown if data length is incorrect.</exception>
    protected override void UpdateFromParameterSpecificBinaryData(byte[] data)
    {
        if (data.Length != 8) // Ensure the data length matches serialization format
            throw new ArgumentException("Invalid data length for EtmaParameter update", nameof(data));

        // Update fields using properties to leverage validation logic
        Length = BitConverter.ToInt32(data, 0);
        SignalStrength = (SignalStrength)BitConverter.ToInt32(data, 4);
    }

    // Static method to create EtmaParameter from binary data
    public static EtmaParameter FromBinary(string name, byte[] data)
    {
        if (data.Length != 8)
            throw new ArgumentException("Invalid data length for EtmaParameter");

        int length = BitConverter.ToInt32(data, 0);
        SignalStrength signalStrength = (SignalStrength)BitConverter.ToInt32(data, 4);

        var param = new EtmaParameter(length, signalStrength);
        return param;
    }

    // Register the type with the factory method
    static EtmaParameter()
    {
        // This will be called when the class is first used
        RegisterType();
    }

    private static void RegisterType()
    {
        // Register this type with the factory
        IndicatorParameterBase.RegisterParameterType(TYPE_ID, (name, data) => EtmaParameter.FromBinary(name, data));
    }

    public override int GetPermutationCount() => TotalPermutations;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_length, LengthRange.Min, LengthRange.Max, LengthRangeStep, "Length"),
            new ParameterDescriptor((int)_signalStrength, SignalStrengthRange.Min, SignalStrengthRange.Max, SignalStrengthRangeStep, "Signal Strength")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Length":
                Length = (int)descriptor.Value;
                break;
            case "Signal Strength":
                SignalStrength = (SignalStrength)(int)descriptor.Value;
                break;
        }
    }

    public int Length
    {
        get => _length;
        set => ValidateAndSetValue(ref _length, value, LengthRange);
    }

    public SignalStrength SignalStrength
    {
        get => _signalStrength;
        set
        {
            int intValue = (int)value;
            ValidateAndSetValue(ref intValue, intValue, SignalStrengthRange);
            _signalStrength = (SignalStrength)intValue;
        }
    }

    public EtmaParameter() : base("etma")
    {
        Length = LengthRange.Min;
        SignalStrength = (SignalStrength)SignalStrengthRange.Min;
    }

    public EtmaParameter(int length, SignalStrength signalStrength) : base("etma")
    {
        Length = length;
        SignalStrength = signalStrength;
    }

    public override bool IncrementSingle()
    {
        int signalStrengthInt = (int)_signalStrength;
        if (IncrementValue(ref signalStrengthInt, SignalStrengthRangeStep, SignalStrengthRange))
        {
            _signalStrength = (SignalStrength)signalStrengthInt;
            return true;
        }
        _signalStrength = (SignalStrength)SignalStrengthRange.Min;

        if (IncrementValue(ref _length, LengthRangeStep, LengthRange))
            return true;

        return false;
    }

    public override EtmaParameter DeepClone()
    {
        return new EtmaParameter(Length, SignalStrength);
    }

    public override EtmaParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in each range
        int lengthSteps = (LengthRange.Max - LengthRange.Min) / LengthRangeStep + 1;
        int signalStrengthSteps = (SignalStrengthRange.Max - SignalStrengthRange.Min) / SignalStrengthRangeStep + 1;

        // Choose a random step for each parameter
        var length = LengthRange.Min + (random.Next(lengthSteps) * LengthRangeStep);
        var signalStrength = (SignalStrength)(SignalStrengthRange.Min + (random.Next(signalStrengthSteps) * SignalStrengthRangeStep));

        return new EtmaParameter(length, signalStrength);
    }

    public override void Reset()
    {
        Length = LengthRange.Min;
        SignalStrength = (SignalStrength)SignalStrengthRange.Min;
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return HashCode.Combine(Length, SignalStrength);
    }
}

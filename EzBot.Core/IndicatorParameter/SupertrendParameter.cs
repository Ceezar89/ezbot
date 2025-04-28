using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EzBot.Core.IndicatorParameter;

public class SupertrendParameter : IndicatorParameterBase
{
    private int _atrPeriod = 20;
    private double _factor = 3.0;

    // Ranges
    private static readonly (int Min, int Max) AtrPeriodRange = (2, 50);
    private static readonly (double Min, double Max) FactorRange = (1.0, 5.0);

    // Steps
    private const int AtrPeriodRangeStep = 2;
    private const double FactorRangeStep = 0.5;

    // Calculate permutations
    private static readonly int AtrPeriodPermutations = CalculateSteps(AtrPeriodRange.Min, AtrPeriodRange.Max, AtrPeriodRangeStep);
    private static readonly int FactorPermutations = CalculateSteps(FactorRange.Min, FactorRange.Max, FactorRangeStep);

    public static readonly int TotalPermutations = AtrPeriodPermutations * FactorPermutations;

    // Type identifier for SupertrendParameter (unique across all parameter types)
    private const byte TYPE_ID = 0x08; // Use a unique ID that doesn't conflict with other parameters

    [Required]
    [Range(5, 20)]
    [JsonPropertyName("atrPeriod")]
    public int AtrPeriod
    {
        get => _atrPeriod;
        set => ValidateAndSetValue(ref _atrPeriod, value, AtrPeriodRange);
    }

    [Required]
    [Range(1.0, 5.0)]
    [JsonPropertyName("factor")]
    public double Factor
    {
        get => _factor;
        set => ValidateAndSetValue(ref _factor, value, FactorRange);
    }

    public SupertrendParameter() : base("supertrend")
    {
        AtrPeriod = AtrPeriodRange.Min;
        Factor = FactorRange.Min;
    }

    private SupertrendParameter(SupertrendParameter parameter) : base(parameter.Name)
    {
        AtrPeriod = parameter.AtrPeriod;
        Factor = parameter.Factor;
    }

    public SupertrendParameter(int atrPeriod, double factor) : base("supertrend")
    {
        AtrPeriod = atrPeriod;
        Factor = factor;
    }

    public override IIndicatorParameter DeepClone()
    {
        return new SupertrendParameter(this);
    }

    protected override byte GetTypeIdentifier() => TYPE_ID;

    protected override byte[] GetParameterSpecificBinaryData()
    {
        byte[] data = new byte[12]; // 4 bytes for AtrPeriod, 8 bytes for Factor

        // Store AtrPeriod (4 bytes)
        BitConverter.GetBytes(_atrPeriod).CopyTo(data, 0);

        // Store Factor (8 bytes)
        BitConverter.GetBytes(_factor).CopyTo(data, 4);

        return data;
    }

    /// <summary>
    /// Updates the instance fields from the parameter-specific binary data.
    /// </summary>
    /// <param name="data">Byte array containing only the specific data for this parameter type.</param>
    /// <exception cref="ArgumentException">Thrown if data length is incorrect.</exception>
    protected override void UpdateFromParameterSpecificBinaryData(byte[] data)
    {
        if (data.Length != 12) // Ensure the data length matches serialization format
            throw new ArgumentException("Invalid data length for SupertrendParameter update", nameof(data));

        // Update fields using properties to leverage validation logic
        AtrPeriod = BitConverter.ToInt32(data, 0);
        Factor = BitConverter.ToDouble(data, 4);
    }

    // Static method to create SupertrendParameter from binary data
    public static SupertrendParameter FromBinary(string name, byte[] data)
    {
        if (data.Length != 12)
            throw new ArgumentException("Invalid data length for SupertrendParameter");

        int atrPeriod = BitConverter.ToInt32(data, 0);
        double factor = BitConverter.ToDouble(data, 4);

        var param = new SupertrendParameter(atrPeriod, factor);
        return param;
    }

    // Register the type with the factory method
    static SupertrendParameter()
    {
        // This will be called when the class is first used
        RegisterType();
    }

    private static void RegisterType()
    {
        // Register this type with the factory
        IndicatorParameterBase.RegisterParameterType(TYPE_ID, (name, data) => SupertrendParameter.FromBinary(name, data));
    }

    public override int GetPermutationCount() => TotalPermutations;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_atrPeriod, AtrPeriodRange.Min, AtrPeriodRange.Max, AtrPeriodRangeStep, "ATR Period"),
            new ParameterDescriptor(_factor, FactorRange.Min, FactorRange.Max, FactorRangeStep, "Factor")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "ATR Period":
                AtrPeriod = (int)descriptor.Value;
                break;
            case "Factor":
                Factor = (double)descriptor.Value;
                break;
        }
    }

    public override bool IncrementSingle()
    {
        if (IncrementValue(ref _factor, FactorRangeStep, FactorRange))
            return true;
        _factor = FactorRange.Min;

        if (IncrementValue(ref _atrPeriod, AtrPeriodRangeStep, AtrPeriodRange))
            return true;

        return false;
    }

    public override IIndicatorParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in each range
        int atrPeriodSteps = (AtrPeriodRange.Max - AtrPeriodRange.Min) / AtrPeriodRangeStep + 1;
        int factorSteps = (int)((FactorRange.Max - FactorRange.Min) / FactorRangeStep) + 1;

        // Choose a random step for each parameter
        var atrPeriod = AtrPeriodRange.Min + (random.Next(atrPeriodSteps) * AtrPeriodRangeStep);
        var factor = FactorRange.Min + (random.Next(factorSteps) * FactorRangeStep);

        return new SupertrendParameter(atrPeriod, factor);
    }

    public override void Reset()
    {
        AtrPeriod = AtrPeriodRange.Min;
        Factor = FactorRange.Min;
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return HashCode.Combine(AtrPeriod, Factor);
    }
}
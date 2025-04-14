using EzBot.Models;
using System.Linq;
using System.Collections.Generic;

namespace EzBot.Core.IndicatorParameter;

public class McGinleyDynamicParameter : IndicatorParameterBase
{
    private int _period = 14;

    // Ranges
    private static readonly (int Min, int Max) PeriodRange = (4, 30);

    // Steps
    private const int PeriodRangeStep = 2;

    // Calculate permutations
    private static readonly int PeriodPermutations = CalculateSteps(PeriodRange.Min, PeriodRange.Max, PeriodRangeStep);

    public static readonly int TotalPermutations = PeriodPermutations;

    // Type identifier for McGinleyDynamicParameter (unique across all parameter types)
    private const byte TYPE_ID = 0x05;

    /// <summary>
    /// Creates an instance from binary data without using the factory
    /// </summary>
    public override McGinleyDynamicParameter FromBinary(byte[] data)
    {
        // Extract name
        int nameLength = BitConverter.ToInt32(data, 1);
        string name = System.Text.Encoding.UTF8.GetString(data, 5, nameLength);

        // Create parameter-specific data array
        byte[] paramData = new byte[data.Length - (5 + nameLength)];
        Array.Copy(data, 5 + nameLength, paramData, 0, paramData.Length);

        return FromBinary(name, paramData);
    }

    protected override byte GetTypeIdentifier() => TYPE_ID;

    protected override byte[] GetParameterSpecificBinaryData()
    {
        byte[] data = new byte[4]; // 4 bytes for Period

        // Store Period (4 bytes)
        BitConverter.GetBytes(_period).CopyTo(data, 0);

        return data;
    }

    // Static method to create McGinleyDynamicParameter from binary data
    public static McGinleyDynamicParameter FromBinary(string name, byte[] data)
    {
        if (data.Length != 4)
            throw new ArgumentException("Invalid data length for McGinleyDynamicParameter");

        int period = BitConverter.ToInt32(data, 0);

        var param = new McGinleyDynamicParameter(period);
        param.Name = name;
        return param;
    }

    // Register the type with the factory method
    static McGinleyDynamicParameter()
    {
        // This will be called when the class is first used
        RegisterType();
    }

    private static void RegisterType()
    {
        // Register this type with the factory
        IndicatorParameterBase.RegisterParameterType(TYPE_ID, (name, data) => FromBinary(name, data));
    }

    public override int GetPermutationCount() => TotalPermutations;

    public override List<ParameterDescriptor> GetProperties()
    {
        return [
            new ParameterDescriptor(_period, PeriodRange.Min, PeriodRange.Max, PeriodRangeStep, "Period")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Period":
                Period = (int)descriptor.Value;
                break;
        }
    }

    public int Period
    {
        get => _period;
        set => ValidateAndSetValue(ref _period, value, PeriodRange);
    }

    public McGinleyDynamicParameter() : base("mcginley_dynamic")
    {
        Period = PeriodRange.Min;
    }

    public McGinleyDynamicParameter(int period) : base("mcginley_dynamic")
    {
        Period = period;
    }

    public override void IncrementSingle()
    {
        if (_period + PeriodRangeStep <= PeriodRange.Max)
        {
            _period += PeriodRangeStep;
        }
    }

    public override McGinleyDynamicParameter DeepClone()
    {
        return new McGinleyDynamicParameter(Period);
    }

    public override McGinleyDynamicParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in the range
        int periodSteps = (PeriodRange.Max - PeriodRange.Min) / PeriodRangeStep + 1;

        // Choose a random step
        var period = PeriodRange.Min + (random.Next(periodSteps) * PeriodRangeStep);

        return new McGinleyDynamicParameter(period);
    }

    public override bool CanIncrement()
    {
        return _period + PeriodRangeStep <= PeriodRange.Max;
    }

    public override void Reset()
    {
        Period = PeriodRange.Min;
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return Period.GetHashCode();
    }
}
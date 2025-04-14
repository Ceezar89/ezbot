using System;
using System.Collections.Generic;
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
    private const byte TYPE_ID = 0x07; // Use a unique ID that doesn't conflict with other parameters

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

    // Static method to create SupertrendParameter from binary data
    public static SupertrendParameter FromBinary(string name, byte[] data)
    {
        if (data.Length != 12)
            throw new ArgumentException("Invalid data length for SupertrendParameter");

        int atrPeriod = BitConverter.ToInt32(data, 0);
        double factor = BitConverter.ToDouble(data, 4);

        var param = new SupertrendParameter(atrPeriod, factor);
        param.Name = name;
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
        IndicatorParameterBase.RegisterParameterType(TYPE_ID, (name, data) => FromBinary(name, data));
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

    public override void IncrementSingle()
    {
        // Increment parameters one by one, starting from the least significant
        // When a parameter is successfully incremented, return immediately

        // Start with the least significant parameter (Factor)
        if (IncrementValue(ref _factor, FactorRangeStep, FactorRange))
            return;

        // Reset Factor and try to increment AtrPeriod
        _factor = FactorRange.Min;
        if (_atrPeriod + AtrPeriodRangeStep <= AtrPeriodRange.Max)
        {
            _atrPeriod += AtrPeriodRangeStep;
        }
    }

    public override bool CanIncrement()
    {
        // Check if any parameter can still be incremented
        if (_factor + FactorRangeStep <= FactorRange.Max)
            return true;

        if (_atrPeriod + AtrPeriodRangeStep <= AtrPeriodRange.Max)
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
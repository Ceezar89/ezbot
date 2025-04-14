using EzBot.Models;
using System.Linq;
using System.Collections.Generic;

namespace EzBot.Core.IndicatorParameter;

public class TdfiParameter : IndicatorParameterBase
{
    private int _lookback = 13;

    // Ranges
    private static readonly (int Min, int Max) LookbackRange = (4, 30);

    // Steps
    private const int LookbackRangeStep = 2;

    // Calculate permutations
    private static readonly int LookbackPermutations = CalculateSteps(LookbackRange.Min, LookbackRange.Max, LookbackRangeStep);

    public static readonly int TotalPermutations = LookbackPermutations;

    // Type identifier for TdfiParameter (unique across all parameter types)
    private const byte TYPE_ID = 0x02;

    /// <summary>
    /// Generates all possible parameter permutations as binary data
    /// </summary>
    /// <returns>List of binary representations of all permutations</returns>
    public static List<byte[]> GenerateAllPermutations()
    {
        return GenerateAllPermutationsBinary(() => new TdfiParameter());
    }

    /// <summary>
    /// Creates an instance from binary data without using the factory
    /// </summary>
    public override TdfiParameter FromBinary(byte[] data)
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
        byte[] data = new byte[4]; // 4 bytes for Lookback

        // Store Lookback (4 bytes)
        BitConverter.GetBytes(_lookback).CopyTo(data, 0);

        return data;
    }

    // Static method to create TdfiParameter from binary data
    public static TdfiParameter FromBinary(string name, byte[] data)
    {
        if (data.Length != 4)
            throw new ArgumentException("Invalid data length for TdfiParameter");

        int lookback = BitConverter.ToInt32(data, 0);

        var param = new TdfiParameter(lookback);
        param.Name = name;
        return param;
    }

    // Register the type with the factory method
    static TdfiParameter()
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
            new ParameterDescriptor(_lookback, LookbackRange.Min, LookbackRange.Max, LookbackRangeStep, "Lookback")
        ];
    }

    public override void UpdateFromDescriptor(ParameterDescriptor descriptor)
    {
        switch (descriptor.Name)
        {
            case "Lookback":
                Lookback = (int)descriptor.Value;
                break;
        }
    }

    public int Lookback
    {
        get => _lookback;
        set => ValidateAndSetValue(ref _lookback, value, LookbackRange);
    }

    // Constants that are not changeable
    public int MmaLength { get { return 13; } }
    public int SmmaLength { get { return 13; } }
    public int NLength { get { return 3; } }
    public double FilterHigh { get { return 0.05; } }
    public double FilterLow { get { return -0.05; } }
    public string MmaMode { get { return "ema"; } }
    public string SmmaMode { get { return "ema"; } }
    public bool UseCrossConfirmation { get { return true; } }
    public bool UseInverse { get { return true; } }

    public TdfiParameter() : base("tdfi")
    {
        Lookback = LookbackRange.Min;
    }

    public TdfiParameter(int lookback) : base("tdfi")
    {
        Lookback = lookback;
    }

    public override void IncrementSingle()
    {
        if (_lookback + LookbackRangeStep <= LookbackRange.Max)
        {
            _lookback += LookbackRangeStep;
        }
    }

    public override TdfiParameter DeepClone()
    {
        return new TdfiParameter(Lookback);
    }

    public override TdfiParameter GetRandomNeighbor(Random random)
    {
        // Calculate how many steps are possible in the range
        int lookbackSteps = (LookbackRange.Max - LookbackRange.Min) / LookbackRangeStep + 1;

        // Choose a random step
        var lookback = LookbackRange.Min + (random.Next(lookbackSteps) * LookbackRangeStep);

        return new TdfiParameter(lookback);
    }

    public override bool CanIncrement()
    {
        return _lookback + LookbackRangeStep <= LookbackRange.Max;
    }

    public override void Reset()
    {
        Lookback = LookbackRange.Min;
    }

    protected override int GetAdditionalHashCodeComponents()
    {
        return Lookback.GetHashCode();
    }
}
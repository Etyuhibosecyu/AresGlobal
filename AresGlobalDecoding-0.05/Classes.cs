using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace AresGlobalMethods005;

[DebuggerDisplay("Length = {Length}")]
public class ArithmeticDecoder : IDisposable
{
	protected readonly BitList bits;
	protected const uint l0 = 0, h0 = uint.MaxValue, firstQtr = (h0 - 1) / 4 + 1, half = firstQtr * 2, thirdQtr = firstQtr * 3;
	protected uint l = l0, h = h0, value;
	protected int pos;

	public int Length => bits.Length;

	public ArithmeticDecoder(byte[] byteList)
	{
		bits = new(byteList);
		value = ReverseBits(bits.GetSmallRange(0, 32));
		pos = Min(32, bits.Length);
	}

	public void Dispose()
	{
		bits.Dispose();
		GC.SuppressFinalize(this);
	}

	public uint ReadEqual(uint cCount)
	{
		uint ol = l, oh = h;
		var c = (uint)((((ulong)value - ol + 1) * cCount - 1) / ((ulong)oh - ol + 1));
		l = (uint)(ol + c * ((ulong)oh - ol + 1) / cCount);
		h = (uint)(ol + (c + 1) * ((ulong)oh - ol + 1) / cCount - 1);
		ReadInternal();
		return c;
	}

	public int ReadPart(List<uint> quickMap)
	{
		uint ol = l, oh = h, divisor = quickMap[^1];
		if (divisor == 0)
			return 0;
		int c;
		var freq = (uint)((((ulong)value - ol + 1) * divisor - 1) / ((ulong)oh - ol + 1));
		for (c = 0; quickMap[c] <= freq; c++) ;
		l = (uint)(ol + ((c == 0) ? 0 : quickMap[c - 1]) * ((ulong)oh - ol + 1) / divisor);
		h = (uint)(ol + quickMap[c] * ((ulong)oh - ol + 1) / divisor - 1);
		ReadInternal();
		return c;
	}

	public int ReadPart(SumSet<uint> set)
	{
		uint ol = l, oh = h, divisor = (uint)set.ValuesSum;
		if (divisor == 0)
			return 0;
		var freq = (uint)((((ulong)value - ol + 1) * divisor - 1) / ((ulong)oh - ol + 1));
		var c = set.IndexOfNotGreaterSum(freq);
		var leftSum = (uint)set.GetLeftValuesSum(set[c].Key, out var frequency);
		l = (uint)(ol + leftSum * ((ulong)oh - ol + 1) / divisor);
		h = (uint)(ol + (uint)(leftSum + frequency) * ((ulong)oh - ol + 1) / divisor - 1);
		ReadInternal();
		return c;
	}

	public int ReadPart(SumList sl)
	{
		uint ol = l, oh = h, divisor = (uint)sl.ValuesSum;
		if (divisor == 0)
			return 0;
		var freq = (uint)((((ulong)value - ol + 1) * divisor - 1) / ((ulong)oh - ol + 1));
		var c = sl.IndexOfNotGreaterSum(freq);
		var leftSum = (uint)sl.GetLeftValuesSum(c, out var frequency);
		l = (uint)(ol + leftSum * ((ulong)oh - ol + 1) / divisor);
		h = (uint)(ol + (uint)(leftSum + frequency) * ((ulong)oh - ol + 1) / divisor - 1);
		ReadInternal();
		return c;
	}

	public bool ReadFibonacci(out uint value)
	{
		value = 0;
		var one = false;
		var sequencePos = 0;
		bool input;
		while (pos < bits.Length)
		{
			input = ReadEqual(2) == 1;
			if (input && one || sequencePos == FibonacciSequence.Length)
				return true;
			else
			{
				if (input)
					value += FibonacciSequence[sequencePos];
				sequencePos++;
				one = input;
			}
		}
		value = 0;
		return false;
	}

	protected void ReadInternal()
	{
		while (true)
		{
			if (h < half)
			{
			}
			else if (l >= half)
			{
				l -= half;
				h -= half;
				value -= half;
			}
			else if (l >= firstQtr && h < thirdQtr)
			{
				l -= firstQtr;
				h -= firstQtr;
				value -= firstQtr;
			}
			else
			{
				break;
			}
			l += l;
			h += h + 1;
			value += value + ReadBit();
		}
	}

	private byte ReadBit()
	{
		if (pos >= bits.Length)
			return 0;
		else
			return (byte)(bits[pos++] ? 1 : 0);
	}

	public static implicit operator ArithmeticDecoder(byte[] x) => new(x);
}

public record struct HuffmanData(int MaxFrequency, int FrequencyCount, List<uint> ArithmeticMap, List<Interval> UniqueList, bool SpaceCodes = false)
{
	public readonly void Deconstruct(out int MaxFrequency, out int FrequencyCount, out List<uint> ArithmeticMap, out List<Interval> UniqueList, out bool SpaceCodes)
	{
		MaxFrequency = this.MaxFrequency;
		FrequencyCount = this.FrequencyCount;
		ArithmeticMap = this.ArithmeticMap;
		UniqueList = this.UniqueList;
		SpaceCodes = this.SpaceCodes;
	}

	public static implicit operator HuffmanData((int MaxFrequency, int FrequencyCount, List<uint> ArithmeticMap, List<Interval> UniqueList) value) => new(value.MaxFrequency, value.FrequencyCount, value.ArithmeticMap, value.UniqueList);

	public static implicit operator HuffmanData((int MaxFrequency, int FrequencyCount, List<uint> ArithmeticMap, List<Interval> UniqueList, bool SpaceCodes) value) => new(value.MaxFrequency, value.FrequencyCount, value.ArithmeticMap, value.UniqueList, value.SpaceCodes);
}

public record struct MethodDataUnit(uint R, uint Max, uint Threshold)
{
	public readonly void Deconstruct(out uint R, out uint Max, out uint Threshold)
	{
		R = this.R;
		Max = this.Max;
		Threshold = this.Threshold;
	}

	public static implicit operator MethodDataUnit((uint R, uint Max, uint Threshold) value) => new(value.R, value.Max, value.Threshold);
}

public record struct LZData(MethodDataUnit Dist, MethodDataUnit Length, uint UseSpiralLengths, MethodDataUnit SpiralLength)
{
	public readonly void Deconstruct(out MethodDataUnit Dist, out MethodDataUnit Length, out uint UseSpiralLengths, out MethodDataUnit SpiralLength)
	{
		Dist = this.Dist;
		Length = this.Length;
		UseSpiralLengths = this.UseSpiralLengths;
		SpiralLength = this.SpiralLength;
	}

	public static implicit operator LZData((MethodDataUnit Dist, MethodDataUnit Length, uint UseSpiralLengths, MethodDataUnit SpiralLength) value) => new(value.Dist, value.Length, value.UseSpiralLengths, value.SpiralLength);
}

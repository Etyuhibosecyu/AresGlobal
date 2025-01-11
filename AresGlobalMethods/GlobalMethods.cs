global using AresGlobalMethods;
global using Corlib.NStar;
global using System;
global using System.IO;
global using System.Text;
global using System.Threading.Tasks;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static AresGlobalMethods.DecodingExtents;
global using static Corlib.NStar.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;
using System.Diagnostics;

namespace AresGlobalMethods;

public static class Global
{
	public static NList<byte> WorkUpDoubleList(NList<ShortIntervalList> input, int tn)
	{
		if (input.Length == 0)
			return [];
		ArithmeticEncoder ar = new();
		ar.WriteCount((uint)input.Length);
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length;
		for (var i = 0; i < input.Length; i++)
		{
			for (var j = 0; j < input[i].Length; j++)
				ar.WritePart(input[i][j]);
			Status[tn]++;
		}
		ar.WriteEqual(1234567890, 4294967295);
		Current[tn] += ProgressBarStep;
		NList<byte> bytes = ar;
		ar.Dispose();
		return bytes;
	}

	public static NList<byte> WorkUpTripleList(List<NList<ShortIntervalList>> input, int tn)
	{
		if (input.Any(x => x.Length == 0))
			return [];
		ArithmeticEncoder ar = new();
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		Status[tn] = 0;
		StatusMaximum[tn] = input.Sum(x => x.Length);
		for (var i = 0; i < input.Length; i++)
		{
			ar.WriteCount((uint)input[i].Length);
			for (var j = 0; j < input[i].Length; j++)
			{
				for (var k = 0; k < input[i][j].Length; k++)
					ar.WritePart(input[i][j][k]);
				Status[tn]++;
			}
		}
		ar.WriteEqual(1234567890, 4294967295);
		Current[tn] += ProgressBarStep;
		NList<byte> bytes = ar;
		ar.Dispose();
		return bytes;
	}

	public static void WriteCount(this ArithmeticEncoder ar, uint originalBase)
	{
		var t = Max(BitsCount(originalBase) - 1, 0);
		ar.WriteEqual((uint)t, 31);
		var t2 = (uint)1 << Max(t, 1);
		ar.WriteEqual(originalBase - ((t == 0) ? 0 : t2), t2);
	}
}

public class Huffman(NList<ShortIntervalList> input, NList<ShortIntervalList> result, int tn)
{
	public NList<ShortIntervalList> Encode()
	{
		if (input.Length == 0)
			throw new EncoderFallbackException();
		var bwtIndex = input[0].IndexOf(BWTApplied);
		if (CreateVar(input[0].IndexOf(HuffmanApplied), out var huffmanIndex) != -1 && (bwtIndex == -1 || huffmanIndex != bwtIndex + 1))
			return input;
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		result.Replace(input);
		result[0] = new(result[0]);
		var lz = CreateVar(input[0].IndexOf(LempelZivApplied), out var lzIndex) != -1 && (bwtIndex == -1 || lzIndex != bwtIndex + 1);
		var lzDummy = CreateVar(input[0].IndexOf(LempelZivDummyApplied), out var lzDummyIndex) != -1 && (bwtIndex == -1 || lzDummyIndex != bwtIndex + 1);
		var bwtLength = bwtIndex != -1 ? (int)input[0][bwtIndex + 1].Base : 0;
		var startPos = (lz || lzDummy ? (input[0].Length >= lzIndex + 2 && input[0][lzIndex + 1] == LempelZivSubdivided ? 3 : 2) : 1) + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : 0) + bwtLength;
		var lzPos = bwtIndex != -1 ? 4 : 2;
		if (input.Length < startPos + 2)
			return input;
		var spaces = input[0].Length >= 2 && input[0][1] == SpacesApplied;
		var innerCount = spaces ? 2 : 1;
		Status[tn] = 0;
		StatusMaximum[tn] = 7;
		var indexedInput = input.GetRange(startPos).ToNList((x, index) => (elem: x[0], index));
		if (lz)
			indexedInput.FilterInPlace(x => x.index < 2 || x.elem.Lower + x.elem.Length != x.elem.Base);
		var groups = indexedInput.Group(x => x.elem.Lower);
		var maxFrequency = groups.Max(x => x.Length);
		var consequentElems = maxFrequency > input[startPos][0].Base * 2 || input[startPos][0].Base <= ValuesInByte + 1;
		if (consequentElems)
			groups.NSort(x => 4294967295 - (uint)x.Length);
		Status[tn]++;
		var uniqueList = groups.PConvert(x => new Interval(x[0].elem) { Base = input[startPos][0].Base });
		Status[tn]++;
		var indexCodes = RedStarLinq.NEmptyList<int>(input.Length - startPos);
		for (var i = 0; i < groups.Length; i++)
			foreach (var (elem, index) in groups[i])
				indexCodes[index] = i;
		Status[tn]++;
		NList<(int elem, int freq)> frequencyTable = groups.PNConvert((x, index) => (index, x.Length));
		groups.Dispose();
		Status[tn]++;
		var frequency = frequencyTable.PNConvert(x => x.freq);
		Status[tn]++;
		var intervalsBase = (uint)frequency.Sum();
		uint a = 0;
		var arithmeticMap = frequency.ToNList(x => a += (uint)x);
		Status[tn]++;
		if (lz)
			intervalsBase = GetBaseWithBuffer(arithmeticMap[^1], spaces);
		var frequencyIntervals = arithmeticMap.Prepend(0u).GetROLSlice(0, arithmeticMap.Length).ToNList((x, index) => new Interval(x, (uint)frequency[index], intervalsBase));
		Status[tn]++;
		Interval lzInterval = lz ? new(arithmeticMap[^1], intervalsBase - arithmeticMap[^1], intervalsBase) : new();
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length - startPos;
		Current[tn] += ProgressBarStep;
		Parallel.For(startPos, input.Length, i =>
		{
			if (lz && i >= startPos + lzPos && result[i][0].Lower + result[i][0].Length == result[i][0].Base)
				result[i] = new(result[i]) { [0] = lzInterval };
			else
				result[i] = new(result[i]) { [0] = i >= startPos + lzPos ? frequencyIntervals[indexCodes[i - startPos]] : new(frequencyIntervals[indexCodes[i - startPos]]) { Base = arithmeticMap[^1] } };
			Status[tn]++;
		});
		indexCodes.Dispose();
		arithmeticMap.Dispose();
		frequencyIntervals.Dispose();
		Status[tn]++;
		NList<Interval> c = [];
		c.WriteCount((uint)maxFrequency - 1);
		c.WriteCount((uint)frequencyTable.Length - 1);
		Status[tn] = 0;
		StatusMaximum[tn] = frequencyTable.Length;
		Current[tn] += ProgressBarStep;
		if (consequentElems)
			for (var i = 0; i < frequencyTable.Length; i++, Status[tn]++)
			{
				c.Add(uniqueList[frequencyTable[i].elem]);
				if (i != 0)
					c.Add(new((uint)frequency[i] - 1, (uint)frequency[i - 1]));
			}
		else
			for (var i = 0; i < frequencyTable.Length; i++, Status[tn]++)
				c.Add(new(frequency[i] >= 1 ? (uint)frequency[i] - 1 : throw new EncoderFallbackException(), (uint)maxFrequency));
		uniqueList.Dispose();
		frequencyTable.Dispose();
		frequency.Dispose();
		var cSplit = c.SplitIntoEqual(8);
		c.Dispose();
		var cLength = (uint)cSplit.Length;
		var insertIndex = lz ? lzIndex : lzDummy ? lzDummyIndex : result[0].Length;
		result[0].Insert(insertIndex, HuffmanApplied);
		result[0].Insert(insertIndex + 1, new(0, cLength, cLength));
		result.Insert(startPos - bwtLength, cSplit.PConvert(x => new ShortIntervalList(x)));
		cSplit.Dispose();
		return result;
	}
}

public class RLE(NList<byte> input, int tn)
{
	public NList<byte> Encode()
	{
		NList<byte> result = [];
		if (input.Length < 1)
			return input;
		InitProgressBars(tn);
		for (var i = 0; i < input.Length;)
		{
			result.Add(input[Status[tn] = i++]);
			if (i == input.Length)
				break;
			var j = i;
			while (i < input.Length && i - j < ValuesIn2Bytes && input[i] == input[i - 1])
				Status[tn] = i++;
			if (i != j)
			{
				result.AddRange(RepeatSerieMarker(i - j));
				continue;
			}
			j = i;
			while (i < input.Length && i - j < ValuesIn2Bytes && input[i] != input[i - 1])
				Status[tn] = i++;
			i--;
			result.AddRange(NoRepeatSerieMarker(i - j)).AddRange(input.GetSlice(j..i));
		}
#if DEBUG
		var decoded = new RLEDec(result).Decode();
		for (var i = 0; i < input.Length && i < decoded.Length; i++)
		{
			var x = input[i];
			var y = decoded[i];
			if (!x.Equals(y))
				throw new DecoderFallbackException();
		}
		if (input.Length != decoded.Length)
			throw new DecoderFallbackException();
#endif
		return result;
	}

	public NList<byte> RLE3(bool updateStatus = true)
	{
		NList<byte> result = [];
		if (input.Length < 3 || input.Length % 3 != 0)
			return input;
		var length = input.Length / 3;
		if (updateStatus)
		{
			Current[tn] = 0;
			CurrentMaximum[tn] = 0;
			Status[tn] = 0;
			StatusMaximum[tn] = length;
		}
		for (var i = 0; i < length;)
		{
			result.AddRange(input.GetSlice(i++ * 3, 3));
			if (updateStatus)
				Status[tn]++;
			if (i == length)
				break;
			var j = i;
			while (i < length && i - j < ValuesIn2Bytes && input.Compare(i * 3, input, (i - 1) * 3, 3) == 3)
			{
				i++;
				if (updateStatus)
					Status[tn]++;
			}
			if (i != j)
			{
				result.AddRange(RepeatSerieMarker(i - j));
				continue;
			}
			j = i;
			while (i < length && i - j < ValuesIn2Bytes && input.Compare(i * 3, input, (i - 1) * 3, 3) != 3)
			{
				i++;
				if (updateStatus)
					Status[tn]++;
			}
			i--;
			result.AddRange(NoRepeatSerieMarker(i - j)).AddRange(input.GetSlice((j * 3)..(i * 3)));
		}
#if DEBUG
		var decoded = new RLEDec(result).DecodeRLE3();
		for (var i = 0; i < input.Length && i < decoded.Length; i++)
		{
			var x = input[i];
			var y = decoded[i];
			if (!x.Equals(y))
				throw new DecoderFallbackException();
		}
		if (input.Length != decoded.Length)
			throw new DecoderFallbackException();
#endif
		return result;
	}

	public NList<byte> RLEMixed()
	{
		NList<byte> result = [];
		if (input.Length < 1)
			return input;
		InitProgressBars(tn);
		for (var i = 0; i < input.Length;)
		{
			result.Add(input[Status[tn] = i++]);
			if (i == input.Length)
				break;
			var j = i;
			while (i < input.Length && i - j < ValuesIn2Bytes && input[i] == input[i - 1])
				Status[tn] = i++;
			if (i >= j + 2)
			{
				result.AddRange(i - j < ValuesInByte >> 2 ? [(byte)(i - j - 1)] : [((ValuesInByte >> 2) - 1), (byte)((i - j - (ValuesInByte >> 2)) >> BitsPerByte), unchecked((byte)(i - j - (ValuesInByte >> 2)))]);
				continue;
			}
			i = j;
			while (i < input.Length - 4 && i - j < ValuesIn2Bytes * 3 && input.Compare(i + 2, input, i - 1, 3) == 3)
				Status[tn] = (i += 3) - 1;
			if (i != j)
			{
				result.AddRange((i - j) / 3 < ValuesInByte >> 2 ? [(byte)((i - j) / 3 - 1 + (ValuesInByte >> 2))] : [((ValuesInByte >> 1) - 1), (byte)(((i - j) / 3 - (ValuesInByte >> 2)) >> BitsPerByte), unchecked((byte)((i - j) / 3 - (ValuesInByte >> 2)))]);
				result.Add(input[i++]).Add(input[i++]);
				continue;
			}
			i = j;
			while (i < input.Length && i - j < ValuesIn2Bytes && (input[i] != input[i - 1] || i < input.Length - 1 && input[i] != input[i + 1]) && (i == j || i >= input.Length - 5 || input.Compare(i + 3, input, i, 3) != 3))
				Status[tn] = i++;
			i--;
			result.AddRange(NoRepeatSerieMarker(i - j)).AddRange(input.GetSlice(j..i));
		}
#if DEBUG
		var decoded = new RLEDec(result).DecodeMixed();
		for (var i = 0; i < input.Length && i < decoded.Length; i++)
		{
			var x = input[i];
			var y = decoded[i];
			if (!x.Equals(y))
				throw new DecoderFallbackException();
		}
		if (input.Length != decoded.Length)
			throw new DecoderFallbackException();
#endif
		return result;
	}

	public NList<byte> RLEN(int n)
	{
		if (input.Length < n || input.Length % n != 0)
			return input;
		NList<byte> result = [];
		var length = input.Length / n;
		Current[tn] = 0;
		CurrentMaximum[tn] = 0;
		Status[tn] = 0;
		StatusMaximum[tn] = length;
		for (var i = 0; i < length;)
		{
			result.AddRange(input.GetSlice(i++ * n, n));
			if (i == length)
				break;
			var j = i;
			while (i < length && i - j < ValuesIn2Bytes && input.Compare(i * n, input, (i - 1) * n, n) == n)
				i++;
			if (i != j)
			{
				result.AddRange(RepeatSerieMarker(i - j));
				continue;
			}
			j = i;
			while (i < length && i - j < ValuesIn2Bytes && input.Compare(i * n, input, (i - 1) * n, n) != n)
				i++;
			i--;
			result.AddRange(NoRepeatSerieMarker(i - j)).AddRange(input.GetSlice((j * n)..(i * n)));
		}
		return result;
	}

	private void InitProgressBars(int tn)
	{
		Current[tn] = 0;
		CurrentMaximum[tn] = 0;
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length;
	}

	private static NList<byte> RepeatSerieMarker(int len)
	{
		if (len < ValuesInByte >> 1)
			return [(byte)(len - 1)];
		else
		{
			var len2 = len - (ValuesInByte >> 1);
			return [((ValuesInByte >> 1) - 1), (byte)(len2 >> BitsPerByte), unchecked((byte)len2)];
		}
	}

	private static NList<byte> NoRepeatSerieMarker(int len)
	{
		if (len + 1 < ValuesInByte >> 1)
			return [(byte)(len + (ValuesInByte >> 1))];
		else
		{
			var len2 = len + 1 - (ValuesInByte >> 1);
			return [(ValuesInByte - 1), (byte)(len2 >> BitsPerByte), unchecked((byte)len2)];
		}
	}
}

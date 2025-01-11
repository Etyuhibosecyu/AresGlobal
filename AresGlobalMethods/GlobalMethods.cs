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
	private bool lz, lzDummy, spaces, consequentElems;
	private int bwtIndex, lzIndex, lzDummyIndex, bwtLength, lzHeaderLength, startPos, lzPos, maxFrequency;
	private NList<Interval> uniqueList = default!;
	private NList<int> indexCodes = default!, frequency = default!;
	private NList<(int elem, int freq)> frequencyTable = default!;

	public NList<ShortIntervalList> Encode()
	{
		if (input.Length == 0)
			throw new EncoderFallbackException();
		bwtIndex = input[0].IndexOf(BWTApplied);
		if (CreateVar(input[0].IndexOf(HuffmanApplied), out var huffmanIndex) != -1 && (bwtIndex == -1 || huffmanIndex != bwtIndex + 1))
			return input;
		Prerequisites();
		if (input.Length < startPos + 2)
			return input;
		spaces = input[0].Length >= 2 && input[0][1] == SpacesApplied;
		Status[tn] = 0;
		StatusMaximum[tn] = 7;
		ProcessGroups(input, tn);
		frequency = frequencyTable.PNConvert(x => x.freq);
		Status[tn]++;
		ProcessArithmeticMaps();
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

	private void Prerequisites()
	{
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		result.Replace(input);
		result[0] = new(result[0]);
		lz = (lzIndex = input[0].IndexOf(LempelZivApplied)) != -1 && (bwtIndex == -1 || lzIndex != bwtIndex + 1);
		lzDummyIndex = input[0].IndexOf(LempelZivDummyApplied);
		lzDummy = lzDummyIndex != -1 && (bwtIndex == -1 || lzDummyIndex != bwtIndex + 1);
		bwtLength = bwtIndex != -1 ? (int)input[0][bwtIndex + 1].Base : 0;
		if (!lz && !lzDummy)
			lzHeaderLength = 1;
		else if (input[0].Length >= lzIndex + 2 && input[0][lzIndex + 1] == LempelZivSubdivided)
			lzHeaderLength = 3;
		else
			lzHeaderLength = 2;
		startPos = lzHeaderLength + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : 0) + bwtLength;
		lzPos = bwtIndex != -1 ? 4 : 2;
	}

	private void ProcessGroups(NList<ShortIntervalList> input, int tn)
	{
		using var indexedInput = input.GetRange(startPos).ToNList((x, index) => (elem: x[0], index));
		if (lz)
			indexedInput.FilterInPlace(x => x.index < 2 || x.elem.Lower + x.elem.Length != x.elem.Base);
		using var groups = indexedInput.Group(x => x.elem.Lower);
		maxFrequency = groups.Max(x => x.Length);
		consequentElems = maxFrequency > input[startPos][0].Base * 2 || input[startPos][0].Base <= ValuesInByte + 1;
		if (consequentElems)
			groups.NSort(x => 4294967295 - (uint)x.Length);
		Status[tn]++;
		uniqueList = groups.PNConvert(x => new Interval(x[0].elem) { Base = input[startPos][0].Base });
		Status[tn]++;
		indexCodes = RedStarLinq.NEmptyList<int>(input.Length - startPos);
		for (var i = 0; i < groups.Length; i++)
			foreach (var (_, index) in groups[i])
				indexCodes[index] = i;
		Status[tn]++;
		frequencyTable = groups.PNConvert((x, index) => (index, x.Length));
		Status[tn]++;
	}

	private void ProcessArithmeticMaps()
	{
		var intervalsBase = (uint)frequency.Sum();
		using var arithmeticMap = GetArithmeticMap(frequency);
		Status[tn]++;
		if (lz)
			intervalsBase = GetBaseWithBuffer(arithmeticMap[^1], spaces);
		using var frequencyIntervals = arithmeticMap.Prepend(0u).GetROLSlice(0, arithmeticMap.Length).ToNList((x, index) => new Interval(x, (uint)frequency[index], intervalsBase));
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
	}

	private static NList<uint> GetArithmeticMap(NList<int> frequency)
	{
		uint a = 0;
		return frequency.ToNList(x => a += (uint)x);
	}
}

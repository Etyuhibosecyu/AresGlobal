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
using LZEntry = System.Collections.Generic.KeyValuePair<uint, (uint dist, uint length, uint spiralLength)>;
using LZStats = (Corlib.NStar.NList<uint> starts, Corlib.NStar.NList<uint> dists, Corlib.NStar.NList<uint> lengths, Corlib.NStar.NList<uint> spiralLengths);
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

public class LempelZiv
{
	private static readonly int threadsCount = Environment.ProcessorCount;
	private readonly NList<ShortIntervalList> input;
	private readonly NList<ShortIntervalList> result;
	private readonly int tn, huffmanIndex;
	private readonly bool huffman, pixels, lw, cout, spaces;
	private LZData lzData;

	public LempelZiv(NList<ShortIntervalList> input, NList<ShortIntervalList> result, int tn, bool cout = false)
	{
		this.input = input;
		this.result = result;
		huffmanIndex = input[0].IndexOf(HuffmanApplied);
		huffman = huffmanIndex != -1;
		pixels = input[0].Length >= 1 && input[0][0] == PixelsApplied;
		lw = input[0].Length >= 2 && input[0][1] == LWApplied;
		spaces = input[0].Length >= 2 && input[0][1] == SpacesApplied;
		this.tn = tn;
		this.cout = cout;
	}

	public NList<ShortIntervalList> Encode(out LZData lzData)
	{
		lzData = new();
		var lzStart = 3 + (huffman ? (int)input[0][huffmanIndex + 1].Base : 0) + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : pixels ? 2 : 0);
		if (input.Length <= lzStart)
			return LempelZivDummy(input);
		if (CreateVar(input[0].IndexOf(LempelZivApplied), out var lzIndex) != -1 && !(huffmanIndex != -1 && lzIndex == huffmanIndex + 1) && !(CreateVar(input[0].IndexOf(BWTApplied), out var bwtIndex) != -1 && lzIndex == bwtIndex + 1))
			return input;
		var result = huffman || input[0].Length >= 1 && input[0][0] == WordsApplied || pixels ? EncodeInts(lzStart) : EncodeBytes(lzStart);
		lzData = this.lzData;
		return result;
	}

	private NList<ShortIntervalList> EncodeInts(int lzStart)
	{
		var complexCodes = huffman && lw ? input.GetRange(lzStart - 2).RepresentIntoNumbers((x, y) => RedStarLinq.Equals(x, y), x => x.Length switch
		{
			0 => 1234567890,
			1 => x[0].GetHashCode(),
			2 => x[0].GetHashCode() << 7 ^ x[1].GetHashCode(),
			_ => (x[0].GetHashCode() << 7 ^ x[1].GetHashCode()) << 7 ^ x[^1].GetHashCode(),
		}).ToNList(x => ((uint)x, 0u)) : input.GetRange(lzStart - 2).ToNList((x, index) => (pixels && !(huffman || cout) ? x[0].Lower << 24 | x[1].Lower << 16 | x[2].Lower << 8 | x[3].Lower : x[0].Lower << 9 ^ x[0].Base, spaces ? x[^1].Lower : !pixels ? 0 : x.Length >= (huffman || cout ? 4 : 7) ? (x[^3].Lower & ValuesInByte >> 1) + (ValuesInByte >> 1) << 9 | x[^2].Lower << 8 | x[^1].Lower : x.Length >= (huffman || cout ? 2 : 5) ? x[^1].Lower : 0));
		var (preIndexCodes, secondaryCodes) = complexCodes.NBreak();
		var secondaryCodesActive = secondaryCodes.Any(x => x != 0);
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		var combined = preIndexCodes.NPairs((x, y) => (ulong)x << 32 | y);
		var indexCodesList = combined.PGroup(tn, new EComparer<ulong>((x, y) => x == y, x => unchecked((17 * 23 + (int)(x >> 32)) * 23 + (int)x))).FilterInPlace(x => x.Group.Length >= 2);
		combined.Dispose();
		var indexCodes = indexCodesList.Sort(x => x.Key).PToArray(col => col.Group.Sort());
		indexCodesList.Dispose();
		var startKGlobal = 2;
		var repeatsInfo = RedStarLinq.FillArray(threadsCount, _ => new Dictionary<uint, (uint dist, uint length, uint spiralLength)>(65));
		uint useSpiralLengths = 0;
		var maxLevel = Max(BitsCount(LZDictionarySize) / 2 - 5, 0);
		Status[tn] = 0;
		StatusMaximum[tn] = indexCodes.Sum(x => x.Length) * (maxLevel + 1);
		Current[tn] += ProgressBarStep;
		var lockObj = RedStarLinq.FillArray(threadsCount, _ => new object());
		Parallel.ForEach(indexCodes, x => FindMatchesRecursive(x, 0));
		complexCodes.Dispose();
		preIndexCodes.Dispose();
		secondaryCodes.Dispose();
		var repeatsInfoSum = repeatsInfo.ConvertAndJoin(x => x).ToNList();
		return WriteLZ(input, lzStart, repeatsInfoSum, useSpiralLengths);
		void FindMatchesRecursive(NList<uint> ic, int level)
		{
			if (level < maxLevel)
			{
				var nextCodes = ic.GetSlice(..(ic[^1] == preIndexCodes.Length - level - startKGlobal ? ^1 : ^0)).GroupIndexes(iIC => preIndexCodes[(int)iIC + level + startKGlobal]).FilterInPlace(x => x.Length >= 2).Convert(x => x.ToNList(index => ic[index]).Sort());
				nextCodes.ForEach(x => FindMatchesRecursive(x, level + 1));
				FindMatches2(ic, level + startKGlobal);
			}
			else if (ic.Length > 1)
				FindMatches(ic, level + startKGlobal);
		}
		void FindMatches(NList<uint> ic, int startK)
		{
			var nextTarget = 0;
			for (var i = 1; i < ic.Length; i++, Status[tn]++)
			{
				var iIC = (int)ic[i];
				if (iIC < nextTarget)
					continue;
				var found = ic.BinarySearch(0, i, (uint)Max(iIC - LZDictionarySize, 0));
				var matches = ic.GetSlice((found >= 0 ? found : ~found)..(ic[i - 1] == ic[i] - 1 ? i - 1 : i)).Filter(jIC => iIC - jIC >= 2 && (!secondaryCodesActive || secondaryCodes.Compare(iIC, secondaryCodes, (int)jIC, startK) == startK)).ToNList();
				var length = 0;
				for (var j = matches.Length - 1; j >= 0; j--)
				{
					var jIC = (int)matches[j];
					if (iIC - jIC < 2)
						continue;
					var k = complexCodes.Compare(iIC + startK, complexCodes, jIC + startK) + startK;
					if (k - 2 <= length)
						continue;
					length = k - 2;
					var product = 1d;
					if (input.GetSlice(iIC + lzStart - 2, k).All(x => (product *= x.Product(y => (double)y.Base / y.Length)) < 21d * LZDictionarySize * k))
						continue;
					var sl = (ushort)Clamp(length / (iIC - jIC) - 1, 0, ushort.MaxValue);
					UpdateRepeatsInfo(repeatsInfo, lockObj, new((uint)(iIC + lzStart - 2), ((uint)Max(iIC - jIC - length - 2, 0), (uint)Min(length, iIC - jIC - 2), sl)));
					if (sl > 0)
					{
						nextTarget = jIC + k;
						useSpiralLengths = 1;
					}
				}
			}
		}
		void FindMatches2(NList<uint> ic, int k)
		{
			for (var i = 1; i < ic.Length; i++, Status[tn]++)
			{
				var iIC = (int)ic[i];
				var jIC = (int)ic[i - 1];
				if (!(iIC - jIC >= 2 && iIC - jIC < LZDictionarySize && (!secondaryCodesActive || secondaryCodes.Compare(iIC, secondaryCodes, jIC, k) == k)))
					continue;
				var product = 1d;
				if (input.GetSlice(iIC + lzStart - 2, k).All(x => (product *= x.Product(y => (double)y.Base / y.Length)) < 21d * LZDictionarySize * k))
					continue;
				var sl = (ushort)Clamp(k / (iIC - jIC) - 1, 0, ushort.MaxValue);
				UpdateRepeatsInfo(repeatsInfo, lockObj, new((uint)(iIC + lzStart - 2), ((uint)Max(iIC - jIC - k, 0), (uint)Min(k - 2, iIC - jIC - 2), sl)));
				if (sl > 0)
					useSpiralLengths = 1;
			}
		}
	}

	private NList<ShortIntervalList> EncodeBytes(int lzStart)
	{
		var preIndexCodes = input.GetRange(lzStart - 2).Convert(x => (byte)x[0].Lower);
		if (preIndexCodes.Length < 5)
			return LempelZivDummy(input);
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 3;
		var combined = preIndexCodes.NCombine(preIndexCodes.GetRange(1), preIndexCodes.GetRange(2), (x, y, z) => ((uint)x << 8 | y) << 8 | z);
		var indexCodesList = combined.PGroup(tn).FilterInPlace(x => x.Group.Length >= 2);
		combined.Dispose();
		var indexCodes = indexCodesList.NSort(x => x.Key).PToArray(col => col.Group.Sort());
		indexCodesList.Dispose();
		var startKGlobal = 3;
		var repeatsInfo = RedStarLinq.FillArray(threadsCount, _ => new Dictionary<uint, (uint dist, uint length, uint spiralLength)>());
		uint useSpiralLengths = 0;
		var maxLevel = Max(BitsCount(LZDictionarySize) / 2 - 5, 0);
		Status[tn] = 0;
		StatusMaximum[tn] = indexCodes.Sum(x => x.Length) * (maxLevel + 1);
		Current[tn] += ProgressBarStep;
		var lockObj = RedStarLinq.FillArray(threadsCount, _ => new object());
		Parallel.ForEach(indexCodes, x => FindMatchesRecursive(x, 0));
		preIndexCodes.Dispose();
		var repeatsInfoSum = repeatsInfo.ConvertAndJoin(x => x).ToNList();
		return WriteLZ(input, lzStart, repeatsInfoSum, useSpiralLengths);
		void FindMatchesRecursive(NList<uint> ic, int level)
		{
			if (level < maxLevel)
			{
				var endIndex = ic[^1] == preIndexCodes.Length - level - startKGlobal ? ^1 : ^0;
				var nextCodes = ic.GetSlice(..endIndex).Group(iIC => preIndexCodes[(int)iIC + level + startKGlobal]).FilterInPlace(x => x.Length >= 2).Convert(x => x.ToNList());
				nextCodes.ForEach(x => FindMatchesRecursive(x, level + 1));
				FindMatches2(ic, level + startKGlobal);
			}
			else if (ic.Length > 1)
				FindMatches(ic, level + startKGlobal);
		}
		void FindMatches(NList<uint> ic, int startK)
		{
			var nextTarget = 0;
			for (var i = 1; i < ic.Length; i++, Status[tn]++)
			{
				var iIC = (int)ic[i];
				if (iIC < nextTarget)
					continue;
				var found = ic.BinarySearch(0, i, (uint)Max(iIC - LZDictionarySize, 0));
				var matches = ic.GetSlice((found >= 0 ? found : ~found)..(ic[i - 1] == ic[i] - 1 ? i - 1 : i));
				var length = 0;
				for (var j = matches.Length - 1; j >= 0; j--)
				{
					var jIC = (int)matches[j];
					if (iIC - jIC < 2)
						continue;
					var k = preIndexCodes.Compare(iIC + startK, preIndexCodes, jIC + startK) + startK;
					if (k - 2 <= length)
						continue;
					length = k - 2;
					var product = 1d;
					if (input.GetSlice(iIC + lzStart - 2, k).All(x => (product *= x.Product(y => (double)y.Base / y.Length)) < 21d * LZDictionarySize * k))
						continue;
					var sl = (ushort)Clamp(length / (iIC - jIC) - 1, 0, ushort.MaxValue);
					UpdateRepeatsInfo(repeatsInfo, lockObj, new((uint)(iIC + lzStart - 2), ((uint)Max(iIC - jIC - length - 2, 0), (uint)Min(length, iIC - jIC - 2), sl)));
					if (sl > 0)
					{
						nextTarget = jIC + k;
						useSpiralLengths = 1;
					}
				}
			}
		}
		void FindMatches2(NList<uint> ic, int k)
		{
			for (var i = 1; i < ic.Length; i++, Status[tn]++)
			{
				var iIC = (int)ic[i];
				var jIC = (int)ic[i - 1];
				if (iIC - jIC < 2 || iIC - jIC >= LZDictionarySize)
					continue;
				var product = 1d;
				if (input.GetSlice(iIC + lzStart - 2, k).All(x => (product *= x.Product(y => (double)y.Base / y.Length)) < 21d * LZDictionarySize * k))
					continue;
				var sl = (ushort)Clamp(k / (iIC - jIC) - 1, 0, ushort.MaxValue);
				UpdateRepeatsInfo(repeatsInfo, lockObj, new((uint)(iIC + lzStart - 2), ((uint)Max(iIC - jIC - k, 0), (uint)Min(k - 2, iIC - jIC - 2), sl)));
				if (sl > 0)
					useSpiralLengths = 1;
			}
		}
	}

	public static void UpdateRepeatsInfo(Dictionary<uint, (uint dist, uint length, uint spiralLength)>[] repeatsInfo, object[] lockObj, LZEntry entry)
	{
		var (key, (dist, length, sl)) = entry;
		if (repeatsInfo[key % threadsCount].TryGetValue(key, out var value))
		{
			if ((length + 2) * (sl + 1) > (value.length + 2) * (value.spiralLength + 1))
				lock (lockObj[key % threadsCount])
					repeatsInfo[key % threadsCount][key] = (dist, length, sl);
			return;
		}
		lock (lockObj[key % threadsCount])
		{
			if (repeatsInfo[key % threadsCount].ContainsKey(key))
				return;
			repeatsInfo[key % threadsCount].TryAdd(key, (dist, length, sl));
		}
	}

	private NList<ShortIntervalList> WriteLZ(NList<ShortIntervalList> input, int lzStart, NList<LZEntry> repeatsInfo, uint useSpiralLengths)
	{
		if (repeatsInfo.Length == 0)
			return LempelZivDummy(input);
		LZRequisites(input.Length, 1, repeatsInfo, out var boundIndex, out var repeatsInfoList, out var stats, out var maxDist, out var maxLength, out var maxSpiralLength, out var rDist, out var thresholdDist, out var rLength, out var thresholdLength, out var rSpiralLength, out var thresholdSpiralLength);
		result.Replace(input);
		result[0] = new(result[0]);
		Status[tn] = 0;
		StatusMaximum[tn] = stats.starts.Length;
		Current[tn] += ProgressBarStep;
		BitList elementsReplaced = new(result.Length, false);
		WriteLZMatches(input, lzStart, useSpiralLengths, (stats.starts[..boundIndex], stats.dists, stats.lengths, stats.spiralLengths), maxDist, maxLength, maxSpiralLength, rDist, thresholdDist, rLength, thresholdLength, rSpiralLength, thresholdSpiralLength, elementsReplaced);
		var sortedRepeatsInfo2 = repeatsInfoList.PConvert(l => l.PNBreak(x => x.Key, x => (x.Value.dist, x.Value.length, x.Value.spiralLength)));
		repeatsInfoList.ForEach(x => x.Dispose());
		repeatsInfoList.Dispose();
		var brokenRepeatsInfo = sortedRepeatsInfo2.PConvert(l => (l.Item1, l.Item2.PNBreak()));
		sortedRepeatsInfo2.ForEach(x => x.Item2.Dispose());
		sortedRepeatsInfo2.Dispose();
		void ProcessBrokenRepeats((NList<uint>, (NList<uint>, NList<uint>, NList<uint>)) x) => WriteLZMatches(input, lzStart, useSpiralLengths, (x.Item1, x.Item2.Item1, x.Item2.Item2, x.Item2.Item3), maxDist, maxLength, maxSpiralLength, rDist, thresholdDist, rLength, thresholdLength, rSpiralLength, thresholdSpiralLength, elementsReplaced, false);
		if (pixels || cout)
			brokenRepeatsInfo.ForEach(ProcessBrokenRepeats);
		else
			Parallel.ForEach(brokenRepeatsInfo, ProcessBrokenRepeats);
		result.FilterInPlace((x, index) => index == 0 || !elementsReplaced[index]);
		elementsReplaced.Dispose();
		result[0].Add(LempelZivApplied);
		NList<Interval> c = [new Interval((uint)rDist, 3)];
		c.WriteCount(maxDist);
		if (rDist != 0)
			c.Add(new(thresholdDist, maxDist + 1));
		c.Add(new Interval((uint)rLength, 3));
		c.WriteCount(maxLength, 16);
		if (rLength != 0)
			c.Add(new(thresholdLength, maxLength + 1));
		if (maxDist == 0 && maxLength == 0)
			c.Add(new(1, 2));
		c.Add(new Interval(useSpiralLengths, 2));
		if (useSpiralLengths == 1)
		{
			c.Add(new Interval((uint)rSpiralLength, 3));
			c.WriteCount(maxSpiralLength, 16);
			if (rSpiralLength != 0)
				c.Add(new(thresholdSpiralLength, maxSpiralLength + 1));
		}
#if DEBUG
		var input2 = input.Skip(lzStart - 2);
		var decoded = new LempelZivDec(result.GetRange(lzStart - 2), true, new(new(rDist, 0, thresholdDist), new(rLength, 0, thresholdLength), useSpiralLengths, new(rSpiralLength, 0, thresholdSpiralLength)), tn).Decode();
		for (var i = 0; i < input2.Length && i < decoded.Length; i++)
			for (var j = 0; j < input2[i].Length && j < decoded[i].Length; j++)
			{
				var x = input2[i][j];
				var y = decoded[i][j];
				if (!(x.Equals(y) || GetBaseWithBuffer(x.Base, spaces || pixels) == y.Base && x.Lower == y.Lower && x.Length == y.Length))
					throw new DecoderFallbackException();
			}
		if (input2.Length != decoded.Length)
			throw new DecoderFallbackException();
#endif
		if (c.Length > 8)
			result[0].Add(LempelZivSubdivided);
		result.Insert(1 + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : pixels ? 2 : 0), c.SplitIntoEqual(8).Convert(x => new ShortIntervalList(x)));
		lzData = new(new((uint)rDist, maxDist, thresholdDist), new((uint)rLength, maxLength, thresholdLength), useSpiralLengths, new((uint)rSpiralLength, maxSpiralLength, thresholdSpiralLength));
		return result;
	}

	public static void LZRequisites(int inputLength, int multiplier, NList<LZEntry> repeatsInfo, out int boundIndex, out List<NList<LZEntry>> repeatsInfoParts, out LZStats stats, out uint maxDist, out uint maxLength, out uint maxSpiralLength, out int rDist, out uint thresholdDist, out int rLength, out uint thresholdLength, out int rSpiralLength, out uint thresholdSpiralLength)
	{
		var sortedRepeatsInfo = repeatsInfo.Sort(x => x.Key).Sort(x => 4294967295 - GetMatchLength(x));
		boundIndex = sortedRepeatsInfo.FindIndex(x => GetMatchLength(x) < multiplier * 10);
		if (boundIndex == -1)
			boundIndex = sortedRepeatsInfo.Length;
		var processorBlockLength = GetArrayLength(inputLength, Environment.ProcessorCount);
		repeatsInfoParts = RedStarLinq.PFill(Environment.ProcessorCount, index => new NList<LZEntry>());
		var repeatsInfoEnd = sortedRepeatsInfo[boundIndex..];
		for (var i = 0; i < repeatsInfoEnd.Length; i++)
		{
			var x = repeatsInfoEnd[i];
			var index = (int)x.Key / processorBlockLength;
			if ((x.Key + GetMatchLength(x) - 1) / processorBlockLength == index)
				repeatsInfoParts[index].Add(x);
		}
		var brokenRepeatsInfo = sortedRepeatsInfo.PNBreak(x => x.Key, x => (x.Value.dist, x.Value.length, x.Value.spiralLength));
		sortedRepeatsInfo.Dispose();
		var (starts, (dists, lengths, spiralLengths)) = (brokenRepeatsInfo.Item1, brokenRepeatsInfo.Item2.PNBreak());
		brokenRepeatsInfo.Item2.Dispose();
		var sumsAndMaximums = new[] { dists, lengths, spiralLengths }.PNConvert(l => (l.Sum(), l.Max())).Wrap(x => (x[0], x[1], x[2]));
		((var distsSum, maxDist), (var lengthsSum, maxLength), (var spiralLengthsSum, maxSpiralLength)) = sumsAndMaximums;
		var mediumDist = (int)Max(distsSum / dists.Length, 1);
		(rDist, thresholdDist) = GetRAndThreshold(dists, mediumDist);
		var mediumLength = (int)Max(lengthsSum / lengths.Length, 1);
		(rLength, thresholdLength) = GetRAndThreshold(lengths, mediumLength);
		var mediumSpiralLength = (int)Max(spiralLengthsSum / spiralLengths.Length, 1);
		(rSpiralLength, thresholdSpiralLength) = GetRAndThreshold(spiralLengths, mediumSpiralLength);
		stats = (starts, dists, lengths, spiralLengths);
	}

	private static (int, uint) GetRAndThreshold(NList<uint> list, int medium)
	{
		var upperCount = list.Count(x => x >= medium);
		if (upperCount <= list.Length / 3)
			return (1, list.Filter(x => x < medium).Max());
		else if (upperCount > list.Length * 2 / 3)
			return (2, list.Filter(x => x >= medium).Min());
		else
			return (0, 0);
	}

	private void WriteLZMatches(NList<ShortIntervalList> input, int lzStart, uint useSpiralLengths, LZStats stats, uint maxDist, uint maxLength, uint maxSpiralLength, int rDist, uint thresholdDist, int rLength, uint thresholdLength, int rSpiralLength, uint thresholdSpiralLength, BitList elementsReplaced, bool changeBase = true)
	{
		double statesNumLog1, statesNumLog2;
		for (var i = 0; i < stats.starts.Length; i++, Status[tn]++)
		{
			uint iDist = stats.dists[i], iLength = stats.lengths[i], iSpiralLength = stats.spiralLengths[i];
			var localMaxLength = (iLength + 2) * (iSpiralLength + 1) - 2;
			var iStart = (int)stats.starts[i];
			var oldBase = input[iStart][0].Base;
			var newBase = GetBaseWithBuffer(oldBase, spaces || pixels);
			statesNumLog1 = 0;
			if (CreateVar(elementsReplaced.IndexOf(true, iStart, Min((int)localMaxLength + 3, elementsReplaced.Length - iStart)), out var replacedIndex) != -1)
			{
				if (iSpiralLength != 0 || replacedIndex < iStart + 3)
					continue;
				iDist += (uint)(iStart + 3 + iLength - replacedIndex);
				localMaxLength = iLength = (uint)(replacedIndex - iStart - 3);
				if (iDist > maxDist) continue;
			}
			for (var k = iStart; k <= iStart + localMaxLength + 1; k++)
				statesNumLog1 += input[k].Sum(x => Log(x.Base) - Log(x.Length));
			statesNumLog2 = Log(newBase) - Log(newBase - oldBase);
			statesNumLog2 += StatesNumLogSum(iDist, rDist, maxDist, thresholdDist, useSpiralLengths);
			statesNumLog2 += StatesNumLogSum(iLength, rLength, maxLength, thresholdLength);
			if (useSpiralLengths == 1 && iLength < localMaxLength)
				statesNumLog2 += StatesNumLogSum(iSpiralLength, rSpiralLength, maxSpiralLength, thresholdSpiralLength);
			if (statesNumLog1 <= statesNumLog2)
				continue;
			ShortIntervalList b = [new(oldBase, newBase - oldBase, newBase)];
			WriteLZValue(b, iLength, rLength, maxLength, thresholdLength);
			var maxDist2 = Min(maxDist, (uint)(iStart - iLength - lzStart));
			if (useSpiralLengths == 0)
				WriteLZValue(b, iDist, rDist, maxDist2, thresholdDist);
			else if (iLength >= localMaxLength)
				WriteLZValue(b, iDist, rDist, maxDist2, thresholdDist, 1);
			else
			{
				if (rDist == 0 || maxDist2 < thresholdDist)
					b.Add(new(maxDist2 + 1, maxDist2 + 2));
				else if (rDist == 1)
				{
					b.Add(new(thresholdDist + 1, thresholdDist + 2));
					b.Add(new(maxDist2 - thresholdDist, maxDist2 - thresholdDist + 1));
				}
				else
				{
					b.Add(new(maxDist2 - thresholdDist + 1, maxDist2 - thresholdDist + 2));
					b.Add(new(thresholdDist, thresholdDist + 1));
				}
			}
			if (useSpiralLengths == 1 && iLength < localMaxLength)
				WriteLZValue(b, iSpiralLength, rSpiralLength, maxSpiralLength, thresholdSpiralLength);
			result[iStart] = new(b);
			elementsReplaced.SetAll(true, iStart + 1, (int)localMaxLength + 1);
		}
		stats.starts.Dispose();
		stats.dists.Dispose();
		stats.lengths.Dispose();
		stats.spiralLengths.Dispose();
		if (!changeBase)
			return;
		Parallel.For(lzStart, result.Length, i =>
		{
			if (!elementsReplaced[i] && (i == result.Length - 1 || !elementsReplaced[i + 1]))
			{
				var first = result[i][0];
				var newBase = GetBaseWithBuffer(first.Base, spaces || pixels);
				result[i] = !pixels && newBase == 269 ? ByteIntervals2[(int)first.Lower] : new(result[i]) { [0] = new(first.Lower, first.Length, newBase) };
			}
		});
	}

	public static double StatesNumLogSum(uint value, int r, uint max, uint threshold, uint extraShift = 0)
	{
		double sum = 0;
		if (r == 0)
			sum += Log(max + 1 + extraShift);
		else if (r == 1)
		{
			if (value <= threshold)
				sum += Log(threshold + 2);
			else
				sum += Log(threshold + 2) + Log(max - threshold + 1 + extraShift);
		}
		else
		{
			if (value >= threshold)
				sum += Log(max - threshold + 2);
			else
				sum += Log(max - threshold + 2) + Log(threshold + extraShift);
		}
		return sum;
	}

	public static void WriteLZValue(ShortIntervalList list, uint value, int r, uint max, uint threshold, uint extraShift = 0)
	{
		if (r == 0 || max < threshold)
			list.Add(new(value, max + 1 + extraShift));
		else if (r == 1)
		{
			if (value <= threshold)
				list.Add(new(value, threshold + 2));
			else
			{
				list.Add(new(threshold + 1, threshold + 2));
				list.Add(new(value - threshold - 1, max - threshold + extraShift));
			}
		}
		else
		{
			if (value >= threshold)
				list.Add(new(value - threshold, max - threshold + 2));
			else
			{
				list.Add(new(max - threshold + 1, max - threshold + 2));
				list.Add(new(value, threshold + extraShift));
			}
		}
	}

	private static uint GetMatchLength(LZEntry x) => (x.Value.length + 2) * (x.Value.spiralLength + 1);

	private NList<ShortIntervalList> LempelZivDummy(NList<ShortIntervalList> input)
	{
		result.Replace(input);
		result[0] = new(result[0]) { LempelZivDummyApplied };
		NList<Interval> list = [new(0, 3)];
		list.WriteCount(0);
		list.Add(new(0, 3));
		list.WriteCount(0, 16);
		list.Add(new(0, 2));
		result.Insert(1 + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : 0), new ShortIntervalList(list));
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
		Current[tn] = 0;
		CurrentMaximum[tn] = 0;
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length;
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
				result.AddRange(i - j < ValuesInByte >> 1 ? [(byte)(i - j - 1)] : [((ValuesInByte >> 1) - 1), (byte)((i - j - (ValuesInByte >> 1)) >> BitsPerByte), unchecked((byte)(i - j - (ValuesInByte >> 1)))]);
				continue;
			}
			j = i;
			while (i < input.Length && i - j < ValuesIn2Bytes && input[i] != input[i - 1])
				Status[tn] = i++;
			i--;
			result.AddRange(i - j + 1 < ValuesInByte >> 1 ? [(byte)(i - j + (ValuesInByte >> 1))] : [(ValuesInByte - 1), (byte)((i - j + 1 - (ValuesInByte >> 1)) >> BitsPerByte), unchecked((byte)(i - j + 1 - (ValuesInByte >> 1)))]).AddRange(input.GetSlice(j..i));
		}
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
				result.AddRange(i - j < ValuesInByte >> 1 ? [(byte)(i - j - 1)] : [((ValuesInByte >> 1) - 1), (byte)((i - j - (ValuesInByte >> 1)) >> BitsPerByte), unchecked((byte)(i - j - (ValuesInByte >> 1)))]);
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
			result.AddRange(i - j + 1 < ValuesInByte >> 1 ? [(byte)(i - j + (ValuesInByte >> 1))] : [(ValuesInByte - 1), (byte)((i - j + 1 - (ValuesInByte >> 1)) >> BitsPerByte), unchecked((byte)(i - j + 1 - (ValuesInByte >> 1)))]).AddRange(input.GetSlice((j * 3)..(i * 3)));
		}
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
				result.AddRange(i - j < ValuesInByte >> 1 ? [(byte)(i - j - 1)] : [((ValuesInByte >> 1) - 1), (byte)((i - j - (ValuesInByte >> 1)) >> BitsPerByte), unchecked((byte)(i - j - (ValuesInByte >> 1)))]);
				continue;
			}
			j = i;
			while (i < length && i - j < ValuesIn2Bytes && input.Compare(i * n, input, (i - 1) * n, n) != n)
				i++;
			i--;
			result.AddRange(i - j + 1 < ValuesInByte >> 1 ? [(byte)(i - j + (ValuesInByte >> 1))] : [(ValuesInByte - 1), (byte)((i - j + 1 - (ValuesInByte >> 1)) >> BitsPerByte), unchecked((byte)(i - j + 1 - (ValuesInByte >> 1)))]).AddRange(input.GetSlice((j * n)..(i * n)));
		}
		return result;
	}
}

﻿
namespace AresGlobalMethods;

public record class AdaptiveHuffman(int TN)
{
	private bool lz;
	private int bwtLength, startPos;
	private uint firstIntervalDist;
	private readonly SumSet<uint> set = [], newItems = [];
	private readonly SumList lengthsSL = [], distsSL = [];

	public NList<byte> Encode(NList<ShortIntervalList> input, LZData lzData)
	{
		if (input.Length < 2)
			throw new EncoderFallbackException();
		using ArithmeticEncoder ar = new();
		if (!EncodeDoubleList(ar, input, lzData))
			throw new EncoderFallbackException();
		ar.WriteEqual(1234567890, 4294967295);
		return ar;
	}

	public NList<byte> Encode(List<NList<ShortIntervalList>> input, LZData[] lzData)
	{
		if (input.GetSlice(..WordsListActualParts).Any(x => x.Length < 2))
			throw new EncoderFallbackException();
		using ArithmeticEncoder ar = new();
		for (var i = 0; i < WordsListActualParts; i++, _ = i < WordsListActualParts ? Methods[TN] += ProgressBarStep : 0)
			if (!EncodeDoubleList(ar, input[i], lzData[i], i))
				throw new EncoderFallbackException();
		input.GetSlice(WordsListActualParts).ForEach(dl => dl.ForEach(l => l.ForEach(x => ar.WritePart(x))));
		ar.WriteEqual(1234567890, 4294967295);
		return ar;
	}

	private bool EncodeDoubleList(ArithmeticEncoder ar, NList<ShortIntervalList> input, LZData lzData, int blockIndex = 1)
	{
		Prerequisites(input);
		ar.WriteCount((uint)input.Length);
		var isWritingSkipped = false;
		for (var i = 0; i < startPos; i++)
			for (var j = 0; j < input[i].Length; j++)
			{
				var x = input[i][j];
				if (i == startPos - bwtLength && j == 2)
				{
					ar.WriteCount(x.Base);
					newItems.Clear();
					newItems.AddSeries((int)x.Base, index => ((uint)index, 1));
					isWritingSkipped = true;
				}
				if (isWritingSkipped)
				{
					var sum = newItems.GetLeftValuesSum(x.Lower, out var newElem);
					ar.WritePart((uint)sum, (uint)newElem, (uint)newItems.ValuesSum);
					newItems.RemoveValue(x.Lower);
				}
				else
					ar.WritePart(x);
			}
		Status[TN]++;
		var newBase = input[startPos + (newItems.Length != 0 ? BWTBlockExtraSize : 0)][0].Base + (lz ? 1u : 0);
		if (bwtLength != 0 && newItems.Length == 0)
		{
			newItems.Clear();
			newItems.AddSeries((int)newBase, index => ((uint)index, 1));
		}
		ar.WriteCount(newBase);
		Status[TN] = 0;
		StatusMaximum[TN] = input.Length - startPos;
		Current[TN] += ProgressBarStep;
		set.Clear();
		if (lz)
		{
			using var streamOfUnits = RedStarLinq.NFill(1, (int)(lzData.Length.R == 0 ? lzData.Length.Max + 1 : lzData.Length.R == 1 ? lzData.Length.Threshold + 2 : lzData.Length.Max - lzData.Length.Threshold + 2));
			lengthsSL.Replace(streamOfUnits);
			streamOfUnits.Remove((int)lzData.UseSpiralLengths + 1);
			distsSL.Replace(streamOfUnits);
			firstIntervalDist = (lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max + 1) + lzData.UseSpiralLengths;
		}
		else
		{
			lengthsSL.Clear();
			distsSL.Clear();
			firstIntervalDist = 0;
		}
		if (lz)
			set.Add((newBase - 1, 1));
		new Encoder(ar, input, lzData, blockIndex, startPos, lz, newBase, set, lengthsSL, distsSL, firstIntervalDist, newItems, TN).MainProcess();
		return true;
	}

	private void Prerequisites(NList<ShortIntervalList> input)
	{
		var bwtIndex = input[0].IndexOf(BWTApplied);
		if (CreateVar(input[0].IndexOf(HuffmanApplied), out var huffmanIndex) != -1 && !(bwtIndex != -1 && huffmanIndex == bwtIndex + 1))
			throw new EncoderFallbackException();
		Current[TN] = 0;
		CurrentMaximum[TN] = ProgressBarStep * 2;
		Status[TN] = 0;
		StatusMaximum[TN] = 3;
		lz = CreateVar(input[0].IndexOf(LempelZivApplied), out var lzIndex) != -1 && (bwtIndex == -1 || lzIndex != bwtIndex + 1);
		var lzDummy = CreateVar(input[0].IndexOf(LempelZivDummyApplied), out var lzDummyIndex) != -1 && (bwtIndex == -1 || lzDummyIndex != bwtIndex + 1);
		bwtLength = bwtIndex != -1 ? (int)input[0][bwtIndex + 1].Base : 0;
		startPos = (lz || lzDummy ? (input[0].Length >= lzIndex + 2 && input[0][lzIndex + 1] == LempelZivSubdivided ? 3 : 2) : 1) + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : 0) + bwtLength;
		Status[TN]++;
		var lzPos = bwtIndex != -1 ? 4 : 2;
		if (input.Length < startPos + lzPos + 1)
			throw new EncoderFallbackException();
		var originalBase = input[startPos + lzPos][0].Base;
		if (!input.GetRange(startPos + lzPos + 1).All((x, index) => bwtIndex != -1 && (index + lzPos + 1) % (BWTBlockSize + 2) is 0 or 1 || x[0].Base == originalBase))
			throw new EncoderFallbackException();
		Status[TN]++;
	}
}

file sealed record class Encoder(ArithmeticEncoder Ar, NList<ShortIntervalList> Input, LZData LZData, int BlockIndex, int StartPos, bool LZ, uint NewBase, SumSet<uint> Set, SumList LengthsSL, SumList DistsSL, uint FirstIntervalDist, SumSet<uint> NewItems, int TN)
{
	private int frequency, fullLength;
	private uint lzLength, lzDist, lzSpiralLength, maxDist, bufferInterval;
	private long sum;
	private uint item;

	public void MainProcess()
	{
		fullLength = DistsSL.Length;
		for (var i = StartPos; i < Input.Length; i++, Status[TN]++)
		{
			item = Input[i][0].Lower;
			if (NewItems.Length != 0 && i < StartPos + BWTBlockExtraSize)
			{
				Ar.WriteEqual(item, ValuesInByte);
				continue;
			}
			sum = Set.GetLeftValuesSum(item, out frequency);
			bufferInterval = Max((uint)Set.Length, 1);
			var fullBase = (uint)(Set.ValuesSum + bufferInterval);
			if (frequency == 0)
				WriteNewItem(fullBase);
			else
				Ar.WritePart((uint)sum, (uint)frequency, fullBase);
			Set.Increase(item);
			lzLength = lzDist = lzSpiralLength = 0;
			EncodeNextIntervals(i);
		}
	}

	private void WriteNewItem(uint fullBase)
	{
		Ar.WritePart((uint)Set.ValuesSum, bufferInterval, fullBase);
		if (BlockIndex != 2)
			Ar.WriteEqual(item, NewBase);
		else if (NewItems.Length != 0)
		{
			var sum = NewItems.GetLeftValuesSum(item, out var newElem);
			Ar.WritePart((uint)sum, (uint)newElem, (uint)NewItems.ValuesSum);
			NewItems.RemoveValue(item);
		}
	}

	private void EncodeNextIntervals(int inputIndex)
	{
		var innerIndex = 1;
		if (LZ && item == NewBase - 1)
		{
			EncodeLength(inputIndex, ref innerIndex);
			maxDist = Min(LZData.Dist.Max, (uint)(fullLength - lzLength - StartPos - 1));
			EncodeDist(inputIndex, ref innerIndex);
			EncodeSpiralLength(inputIndex, innerIndex);
			var fullLengthDelta = (lzLength + 2) * (lzSpiralLength + 1);
			fullLength += (int)fullLengthDelta;
			if (DistsSL.Length < FirstIntervalDist)
				new Chain((int)Min(FirstIntervalDist - DistsSL.Length, fullLengthDelta)).ForEach(x => DistsSL.Insert(DistsSL.Length - ((int)LZData.UseSpiralLengths + 1), 1));
		}
		else if (LZ)
		{
			fullLength++;
			if (DistsSL.Length < FirstIntervalDist)
				DistsSL.Insert(DistsSL.Length - ((int)LZData.UseSpiralLengths + 1), 1);
		}
		for (; innerIndex < Input[inputIndex].Length; innerIndex++)
			Ar.WritePart(Input[inputIndex][innerIndex]);
	}

	private void EncodeLength(int inputIndex, ref int innerIndex)
	{
		item = Input[inputIndex][innerIndex].Lower;
		lzLength = item + (LZData.Length.R == 2 ? LZData.Length.Threshold : 0);
		sum = LengthsSL.GetLeftValuesSum((int)item, out frequency);
		Ar.WritePart((uint)sum, (uint)frequency, (uint)LengthsSL.ValuesSum);
		LengthsSL.Increase((int)item);
		innerIndex++;
		if (LZData.Length.R != 0 && item == LengthsSL.Length - 1)
		{
			Ar.WritePart(Input[inputIndex][innerIndex]);
			lzLength = Input[inputIndex][innerIndex].Lower + (LZData.Length.R == 2 ? 0 : LZData.Length.Threshold + 1);
			innerIndex++;
		}
	}

	private void EncodeDist(int inputIndex, ref int innerIndex)
	{
		item = Input[inputIndex][innerIndex].Lower;
		var addThreshold = maxDist >= LZData.Dist.Threshold;
		lzDist = item + (LZData.Dist.R == 2 && addThreshold ? LZData.Dist.Threshold : 0);
		if (LZData.Dist.R == 2 && addThreshold && lzDist == maxDist + 1)
		{
			innerIndex++;
			if (Input[inputIndex][innerIndex].Lower != LZData.Dist.Threshold)
				lzDist = Input[inputIndex][innerIndex].Lower;
		}
		sum = DistsSL.GetLeftValuesSum((int)lzDist, out frequency);
		Ar.WritePart((uint)sum, (uint)frequency, (uint)DistsSL.ValuesSum);
		DistsSL.Increase((int)lzDist);
		if (LZData.Dist.R == 1 && addThreshold && lzDist == LZData.Dist.Threshold + 1)
		{
			innerIndex++;
			lzDist = LZData.Dist.Threshold + Input[inputIndex][innerIndex].Lower + 1;
			Ar.WritePart(Input[inputIndex][innerIndex]);
		}
		innerIndex++;
	}

	private void EncodeSpiralLength(int inputIndex, int innerIndex)
	{
		if (LZData.UseSpiralLengths != 0 && Input[inputIndex][innerIndex - 1].Lower == Input[inputIndex][innerIndex - 1].Base - 1)
		{
			lzSpiralLength = Input[inputIndex][^1].Lower;
			if (LZData.SpiralLength.R == 1 != (innerIndex == Input[inputIndex].Length - 1))
				lzSpiralLength += LZData.SpiralLength.Threshold + 2 - LZData.SpiralLength.R;
		}
		else
			lzSpiralLength = 0;
	}
}

public record class AdaptiveHuffmanBits(int TN)
{
	public bool Encode(ArithmeticEncoder ar, NList<ShortIntervalList> input, int startPos)
	{
		if (!(input.Length >= startPos + 2 && input.GetRange(startPos).All(x => x.Length > 0 && x[0].Base == 2)))
			throw new EncoderFallbackException();
		Status[TN] = 0;
		StatusMaximum[TN] = 3;
		Current[TN] += ProgressBarStep;
		Status[TN]++;
		ar.WriteCount((uint)input.Length);
		for (var i = 0; i < startPos; i++)
			for (var j = 0; j < input[i].Length; j++)
				ar.WritePart(input[i][j]);
		Status[TN]++;
		var newBase = input[startPos][0].Base;
		ar.WriteCount(newBase);
		Status[TN] = 0;
		StatusMaximum[TN] = input.Length - startPos;
		Current[TN] += ProgressBarStep;
		var windowSize = 1 << 13;
		uint zeroFreq = 1, totalFreq = 2;
		for (var i = startPos; i < input.Length; i++, Status[TN]++)
		{
			var item = input[i][0].Lower == 1;
			var sum = item ? zeroFreq : 0;
			ar.WritePart(sum, item ? totalFreq - zeroFreq : zeroFreq, totalFreq);
			if (i < windowSize + startPos)
			{
				if (!item)
					zeroFreq++;
				totalFreq++;
			}
			if (i >= windowSize + startPos && input[i - windowSize][0].Lower == (item ? 0u : 1))
			{
				if (item)
					zeroFreq--;
				else
					zeroFreq++;
			}
			for (var j = 1; j < input[i].Length; j++)
				ar.WritePart(input[i][j]);
		}
		return true;
	}
}

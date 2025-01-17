﻿
namespace AresGlobalMethods;

public class AdaptiveHuffmanDec : IDisposable
{
	protected GlobalDecoding decoding = default!;
	protected ArithmeticDecoder ar = default!;
	protected NList<ShortIntervalList> result = default!;
	protected NList<uint> skipped = default!;
	protected SumSet<uint> set = default!, newItems = default!;
	protected NList<Interval> uniqueList = default!;
	protected LZData lzData = default!;
	protected uint fileBase, nextWordLink;
	protected int lz, bwt, blockIndex, fullLength, bwtBlockSize, bwtBlockExtraSize, counter;
	protected SumList lengthsSL, distsSL;
	protected int lzLength;
	protected uint firstIntervalDist;

	public AdaptiveHuffmanDec(GlobalDecoding decoding, ArithmeticDecoder ar, NList<uint> skipped, LZData lzData, int lz, int bwt, int blockIndex, int bwtBlockSize, int counter)
	{
		this.decoding = decoding;
		this.ar = ar;
		this.skipped = skipped;
		this.lzData = lzData;
		this.lz = lz;
		this.bwt = bwt;
		this.blockIndex = blockIndex;
		this.bwtBlockSize = bwtBlockSize;
		bwtBlockExtraSize = bwtBlockSize <= 0x4000 ? 2 : bwtBlockSize <= 0x400000 ? 3 : bwtBlockSize <= 0x40000000 ? 4 : 5;
		this.counter = counter;
		Prerequisites();
		if (lz != 0)
		{
			(lengthsSL = []).AddSeries(1, (int)(lzData.Length.R == 0 ? lzData.Length.Max + 1 : lzData.Length.R == 1 ? lzData.Length.Threshold + 2 : lzData.Length.Max - lzData.Length.Threshold + 2));
			(distsSL = []).AddSeries(1, (int)lzData.UseSpiralLengths + 1);
			firstIntervalDist = (lzData.Dist.R == 1 ? lzData.Dist.Threshold + 2 : lzData.Dist.Max + 1) + lzData.UseSpiralLengths;
		}
		else
		{
			lengthsSL = [];
			distsSL = [];
			firstIntervalDist = 0;
		}
	}

	public virtual void Dispose()
	{
		set?.Dispose();
		uniqueList?.Dispose();
		lengthsSL?.Dispose();
		distsSL?.Dispose();
		GC.SuppressFinalize(this);
	}

	public virtual NList<ShortIntervalList> Decode()
	{
		Prerequisites2();
		for (; counter > 0; counter--, Status[0]++)
			DecodeIteration();
		Current[0] += ProgressBarStep;
		return Postrequisites();
	}

	protected virtual void Prerequisites()
	{
		if (bwt != 0 && blockIndex != 0)
			DecodeSkipped();
		fileBase = ar.ReadNumber();
		if (counter < 0 || counter > GetFragmentLength() + (bwt == 0 ? 0 : GetFragmentLength() >> 8))
			throw new DecoderFallbackException();
		Status[0] = 0;
		StatusMaximum[0] = counter;
		set = [(uint.MaxValue, 1)];
	}

	protected virtual void DecodeSkipped()
	{
		var skippedCount = (int)ar.ReadNumber();
		var @base = skippedCount == 0 ? 1 : ar.ReadNumber();
		(newItems = []).AddSeries((int)@base, index => ((uint)index, 1));
		if (skippedCount > @base || @base > (blockIndex == 2 ? GetFragmentLength() : ValuesInByte))
			throw new DecoderFallbackException();
		for (var i = 0; i < skippedCount; i++)
		{
			skipped.Add(newItems[ar.ReadPart(newItems)].Key);
			newItems.RemoveValue(skipped[^1]);
		}
		counter -= skippedCount == 0 ? 1 : (skippedCount + 9) / 8;
	}

	protected virtual void Prerequisites2()
	{
		uniqueList = [];
		if (lz != 0)
		{
			set.Add((fileBase - 1, 1));
			uniqueList.Add(new(fileBase - 1, fileBase));
		}
		result = [];
	}

	protected virtual void DecodeIteration()
	{
		var readItem = ReadFirst();
		if (!(lz != 0 && uniqueList[readItem].Lower == fileBase - 1))
		{
			result.Add(bwt == 0 && blockIndex == 2 ? new() { uniqueList[readItem], new(ar.ReadEqual(2), 2) } : bwt != 0 && blockIndex != 0 && result.Length < bwtBlockExtraSize ? [new((uint)readItem, ValuesInByte)] : new() { uniqueList[readItem] });
			lzLength++;
			if (lz != 0 && distsSL.Length < firstIntervalDist)
				distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1);
			return;
		}
		decoding.ProcessLZLength(lzData, lengthsSL, out readItem, out var length);
		decoding.ProcessLZDist(lzData, distsSL, result.Length, out readItem, out var dist, length, out var maxDist);
		decoding.ProcessLZSpiralLength(lzData, ref dist, out var spiralLength, maxDist);
		var start = (int)(result.Length - dist - length - 2);
		if (start < 0)
			throw new DecoderFallbackException();
		for (var k = (int)((length + 2) * (spiralLength + 1)); k > 0; k -= (int)length + 2)
			result.AddRange(result.GetSlice(start, (int)Min(length + 2, k)));
		lzLength++;
		if (lz != 0 && distsSL.Length < firstIntervalDist)
			new Chain((int)Min(firstIntervalDist - distsSL.Length, (length + 2) * (spiralLength + 1))).ForEach(x => distsSL.Insert(distsSL.Length - ((int)lzData.UseSpiralLengths + 1), 1));
	}

	protected virtual int ReadFirst()
	{
		if (bwt != 0 && blockIndex != 0 && result.Length < bwtBlockExtraSize)
			return (int)ar.ReadEqual(ValuesInByte);
		var readItem = ar.ReadPart(set);
		if (readItem == set.Length - 1)
			readItem = ReadNewItem();
		else
			set.Increase(uniqueList[readItem].Lower);
		FirstUpdateSet();
		return readItem;
	}

	protected virtual int ReadNewItem()
	{
		uint actualIndex;
		if (bwt != 0 && blockIndex == 2)
		{
			actualIndex = newItems[ar.ReadPart(newItems)].Key;
			newItems.RemoveValue(actualIndex);
		}
		else
			actualIndex = blockIndex != 2 ? ar.ReadEqual(fileBase) : nextWordLink++;
		if (!set.TryAdd((actualIndex, 1), out var readIndex))
			throw new DecoderFallbackException();
		uniqueList.Insert(readIndex, new Interval(actualIndex, fileBase));
		return readIndex;
	}

	protected virtual void FirstUpdateSet() => set.Update(uint.MaxValue, Max(set.Length - 1, 1));

	protected virtual NList<ShortIntervalList> Postrequisites() => bwt != 0 && blockIndex == 2 ? result.ToNList((x, index) => index < bwtBlockExtraSize ? x : [new(x[0].Lower / 2, fileBase / 2), new(x[0].Lower % 2, 2)]) : result;
}

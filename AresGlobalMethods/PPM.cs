
namespace AresGlobalMethods;

public record class PPM(List<NList<ShortIntervalList>> Input, int TN) : IDisposable
{
	private ArithmeticEncoder[] ar = default!;
	private readonly List<NList<Interval>> outputIntervals = [];
	private int doubleListsCompleted = 0, BlocksCount = 0;
	private readonly object lockObj = new();

	public virtual void Dispose()
	{
		ar?.ForEach(x => x?.Dispose());
		outputIntervals?.Dispose();
		GC.SuppressFinalize(this);
	}

	public NList<byte> Encode(bool split = false)
	{
		if (!(Input.Length >= 3 && Input.GetSlice(..3).All(x => x.Length >= 4) || split))
			throw new EncoderFallbackException();
		BlocksCount = split ? Input.Length : WordsListActualParts;
		Current[TN] = 0;
		CurrentMaximum[TN] = ProgressBarStep * (BlocksCount - 1);
		ar = RedStarLinq.FillArray(split ? Input.Length : 1, _ => new ArithmeticEncoder());
		outputIntervals.Replace(RedStarLinq.FillArray(BlocksCount, _ => new NList<Interval>()));
		if (!split)
			Parallel.For(0, BlocksCount, i => EncodeBlock(i, i, i == WordsListActualParts - 1, TN));
		else if (Threads.Count(x => x != null && x.ThreadState is System.Threading.ThreadState.Running
			or System.Threading.ThreadState.Background) == 1 && BlocksCount <= ProgressBarGroups)
			Parallel.For(0, BlocksCount, i => EncodeBlock(i, 1, true, i));
		else
			for (var i = 0; i < BlocksCount; i++)
				EncodeBlock(i, 1, true, TN);
		return split ? ToBytesSplit() : ToBytesNoSplit();
	}

	private void EncodeBlock(int i, int BlockIndex, bool LastBlock, int TN)
	{
		if (!new Encoder(Input[i], outputIntervals[i], BlockIndex, LastBlock, TN).Encode())
			throw new EncoderFallbackException();
		lock (lockObj)
		{
			doubleListsCompleted++;
			if (doubleListsCompleted != BlocksCount)
				Current[TN] += ProgressBarStep;
		}
	}

	private NList<byte> ToBytesSplit()
	{
		Parallel.For(0, BlocksCount, i =>
		{
			outputIntervals[i].ForEach(x => ar[i].WritePart(x));
			ar[i].WriteEqual(1234567890, 4294967295);
		});
		NList<byte> result = [(byte)(BlocksCount - 1)];
		for (var i = 0; i < BlocksCount; i++)
		{
			NList<byte> bytes = ar[i];
			if (i != BlocksCount - 1)
			{
				result.Add((byte)(bytes.Length >> (BitsPerByte << 1)));
				result.Add(unchecked((byte)(bytes.Length >> BitsPerByte)));
				result.Add(unchecked((byte)bytes.Length));
			}
			result.AddRange(bytes);
			bytes.Dispose();
		}
		return result;
	}

	private NList<byte> ToBytesNoSplit()
	{
		outputIntervals.ForEach(l => l.ForEach(x => ar[0].WritePart(x)));
		Input.GetSlice(BlocksCount).ForEach(dl => dl.ForEach(l => l.ForEach(x => ar[0].WritePart(x))));
		ar[0].WriteEqual(1234567890, 4294967295);
		return ar[0];
	}
}

file record class Encoder(NList<ShortIntervalList> Input, NList<Interval> Result, int BlockIndex, bool LastBlock, int TN) : IDisposable
{
	private const int LZDictionarySize = 8388607;
	private int startPos = 1;
	private uint inputBase, item;
	private ShortIntervalList appliedMethods = default!;
	private readonly SumSet<uint> globalFreqTable = [], newItemsFreqTable = [];
	private const int maxContextDepth = 12;
	private readonly LimitedQueue<NList<Interval>> preOutputBuffer = new(maxContextDepth);
	private G.IEqualityComparer<NList<uint>> comparer = default!;
	private FastDelHashSet<NList<uint>> contextSet = default!;
	private HashList<int> lzBuffer = default!;
	private readonly List<SumSet<uint>> contextFreqTableByLevel = [];
	private readonly SumSet<uint> lzPositions = [(uint.MaxValue, 100)];
	private readonly SumList lzLengths = [];
	private uint lzCount, notLZCount, spaceCount, notSpaceCount;
	private readonly LimitedQueue<bool> spaceBuffer = new(maxContextDepth);
	private readonly LimitedQueue<uint> newItemsBuffer = new(maxContextDepth);
	private readonly NList<uint> currentContext = new(maxContextDepth), reservedContext = new(maxContextDepth);
	private readonly SumSet<uint> freqTable = [], excludingFreqTable = [];
	private SumSet<uint> outputFreqTable = [];
	private readonly NList<Interval> intervalsForBuffer = [];
	private int lzBufferIndex, lzBlockEnd = 0;

	public virtual void Dispose()
	{
		globalFreqTable?.Dispose();
		newItemsFreqTable?.Dispose();
		preOutputBuffer?.ForEach(output => output?.Dispose());
		preOutputBuffer?.Dispose();
		contextSet?.ForEach(output => output?.Dispose());
		contextSet?.Dispose();
		lzBuffer?.Dispose();
		contextFreqTableByLevel?.ForEach(output => output?.Dispose());
		contextFreqTableByLevel?.Dispose();
		lzPositions?.Dispose();
		lzLengths?.Dispose();
		spaceBuffer?.Dispose();
		newItemsBuffer?.Dispose();
		currentContext?.Dispose();
		reservedContext?.Dispose();
		freqTable?.Dispose();
		excludingFreqTable?.Dispose();
		outputFreqTable?.Dispose();
		intervalsForBuffer?.Dispose();
		GC.SuppressFinalize(this);
	}

	public bool Encode()
	{
		Initialize();
		for (var i = startPos; i < Input.Length; i++, _ = LastBlock ? Status[TN] = i : 0)
		{
			item = Input[i][0].Lower;
			FormContexts(i);
			intervalsForBuffer.Clear();
			ProcessContexts(i);
			var contextDepth = reservedContext.Length;
			lzBufferIndex = -1;
			Increase();
			if (contextDepth == maxContextDepth)
				lzBuffer.SetOrAdd((i - startPos - maxContextDepth) % LZDictionarySize, lzBufferIndex);
		}
		while (preOutputBuffer.Length != 0)
			preOutputBuffer.Dequeue().ForEach(x => Result.Add(new(x.Lower, x.Length, x.Base)));
		return true;
	}

	private void Initialize()
	{
		EnsureValidInput();
		SetupStatus();
		WriteHeader();
		PrepareFields();
	}

	private void EnsureValidInput()
	{
		if (Input.Length < 4)
			throw new EncoderFallbackException();
		appliedMethods = Input[0];
		startPos = appliedMethods.Length >= 1 && appliedMethods[0] == LengthsApplied ? (int)appliedMethods[1].Base + 1 : 1;
		if (Input.Length <= startPos)
			throw new EncoderFallbackException();
		var firstActual = Input[startPos];
		if (firstActual.Length is not 1 and not 2 || firstActual[0].Length != 1)
			throw new EncoderFallbackException();
		inputBase = firstActual[0].Base;
		if (inputBase < 2 || firstActual[^1].Length != 1)
			throw new EncoderFallbackException();
		var restOfInput = Input.GetRange(startPos + 1);
		if (!restOfInput.All(x => x.Length == Input[startPos].Length && x[0].Length == 1
			&& x[0].Base == inputBase && (x.Length == 1 || x[1].Length == 1 && x[1].Base == Input[startPos][1].Base)))
			throw new EncoderFallbackException();
	}

	private void SetupStatus()
	{
		if (LastBlock)
		{
			Status[TN] = 0;
			StatusMaximum[TN] = Input.Length - startPos;
		}
	}

	private void WriteHeader()
	{
		for (var i = 0; i < appliedMethods.Length; i++)
			Result.Add(new(appliedMethods[i].Lower, appliedMethods[i].Length, appliedMethods[i].Base));
		if (BlockIndex == 0)
		{
			Result.Add(new(Input[1][0].Lower, 1, 3));
			Result.WriteCount(inputBase);
			for (var i = 2; i < startPos; i++)
				for (var j = 0; j < Input[i].Length; j++)
					Result.Add(new(Input[i][j].Lower, Input[i][j].Length, Input[i][j].Base));
		}
		Result.WriteCount((uint)(Input.Length - startPos));
		Result.WriteCount((uint)Min(LZDictionarySize, FragmentLength));
	}

	private void PrepareFields()
	{
		globalFreqTable.Clear();
		if (BlockIndex == 2)
			newItemsFreqTable.Clear();
		else
			newItemsFreqTable.Replace(new Chain((int)inputBase).Convert(x => ((uint)x, 1)));
		preOutputBuffer.Clear();
		comparer = BlockIndex == 2 ? new NListEComparer<uint>() : new EComparer<NList<uint>>((x, y) => x.Equals(y),
			x => unchecked(x.Progression(17 * 23 + x.Length, (x, y) => x * 23 + y.GetHashCode())));
		contextSet = new(comparer);
		lzBuffer = [];
		contextFreqTableByLevel.Clear();
		lzLengths.Replace([1]);
		lzCount = notLZCount = spaceCount = notSpaceCount = 1;
		spaceBuffer.Clear();
		newItemsBuffer.Clear();
		currentContext.Clear();
		reservedContext.Clear();
		freqTable.Clear();
		excludingFreqTable.Clear();
		intervalsForBuffer.Clear();
		lzBlockEnd = 0;
	}

	private void FormContexts(int i)
	{
		for (int j = Max(startPos, i - maxContextDepth), index = 0; j < i; j++, index++)
			currentContext.SetOrAdd(index, Input[j][0].Lower);
		currentContext.Reverse();
		reservedContext.Replace(currentContext);
	}

	private void ProcessContexts(int i)
	{
		if (i < lzBlockEnd)
			return;
		if (currentContext.Length != maxContextDepth || i < (maxContextDepth << 1) + startPos || !TryProcessLZ(currentContext, i) || i >= lzBlockEnd)
		{
			freqTable.Clear();
			excludingFreqTable.Clear();
			SkipTrivialContexts();
			var prediction = GetPrediction();
			UpdateFreqTables();
			ProcessFrequency(prediction);
			ProcessBuffers(i);
		}
	}

	private void SkipTrivialContexts()
	{
		for (; currentContext.Length > 0 && !contextSet.TryGetIndexOf(currentContext, out _); currentContext.RemoveAt(^1))
			;
	}

	private PredictionEntry GetPrediction()
	{
		long sum = 0;
		var frequency = 0;
		while (true)
		{
			if (currentContext.Length <= 0 || !contextSet.TryGetIndexOf(currentContext, out var index))
				break;
			freqTable.Replace(contextFreqTableByLevel[index]);
			freqTable.ExceptWith(excludingFreqTable);
			if ((sum = freqTable.GetLeftValuesSum(item, out frequency)) < 0 || frequency != 0)
				break;
			if (freqTable.Length != 0)
				intervalsForBuffer.Add(new((uint)freqTable.ValuesSum, (uint)freqTable.Length * 100, GetFreqTableBase(freqTable)));
			currentContext.RemoveAt(^1);
			excludingFreqTable.UnionWith(freqTable);
		}
		return new(sum, frequency);
	}

	private void UpdateFreqTables()
	{
		if (freqTable.Length == 0 || currentContext.Length == 0)
		{
			foreach (var (Key, _) in excludingFreqTable)
				excludingFreqTable.Update(Key, globalFreqTable.TryGetValue(Key, out var newValue) ? newValue : throw new EncoderFallbackException());
			outputFreqTable = globalFreqTable.ExceptWith(excludingFreqTable);
		}
		else
			outputFreqTable = freqTable;
	}

	private void ProcessFrequency(PredictionEntry prediction)
	{
		var (sum, frequency) = prediction;
		if (frequency == 0)
			sum = outputFreqTable.GetLeftValuesSum(item, out frequency);
		if (frequency == 0)
			ProcessNewItem();
		else
		{
			intervalsForBuffer.Add(new(0, (uint)outputFreqTable.ValuesSum, GetFreqTableBase(outputFreqTable)));
			intervalsForBuffer.Add(new((uint)sum, (uint)frequency, (uint)outputFreqTable.ValuesSum));
			newItemsBuffer.Enqueue(uint.MaxValue);
		}
		if (freqTable.Length == 0 || currentContext.Length == 0)
			globalFreqTable.UnionWith(excludingFreqTable);
	}

	private void ProcessNewItem()
	{
		if (outputFreqTable.Length != 0)
			intervalsForBuffer.Add(new((uint)outputFreqTable.ValuesSum, (uint)outputFreqTable.Length * 100, GetFreqTableBase(outputFreqTable)));
		if (BlockIndex != 2)
		{
			intervalsForBuffer.Add(new((uint)newItemsFreqTable.IndexOf(item), (uint)newItemsFreqTable.Length));
			newItemsFreqTable.RemoveValue(item);
			newItemsBuffer.Enqueue(item);
		}
	}

	private void ProcessBuffers(int i)
	{
		var isSpace = false;
		if (BlockIndex == 2)
		{
			isSpace = Input[i][1].Lower != 0;
			uint bufferSpaces = (uint)spaceBuffer.Count(true), bufferNotSpaces = (uint)spaceBuffer.Count(false);
			intervalsForBuffer.Add(new(isSpace ? notSpaceCount + bufferNotSpaces : 0, isSpace ? spaceCount + bufferSpaces : notSpaceCount + bufferNotSpaces, notSpaceCount + spaceCount + (uint)spaceBuffer.Length));
		}
		else
			for (var j = 1; j < Input[i].Length; j++)
				intervalsForBuffer.Add(new(Input[i][j].Lower, Input[i][j].Length, Input[i][j].Base));
		if (preOutputBuffer.IsFull)
			preOutputBuffer.Dequeue().ForEach(x => Result.Add(new(x.Lower, x.Length, x.Base)));
		preOutputBuffer.Enqueue(intervalsForBuffer.Copy());
		ProcessSpaceCount();
		spaceBuffer.Enqueue(isSpace);
	}

	private void ProcessSpaceCount()
	{
		if (BlockIndex == 2 && spaceBuffer.IsFull)
		{
			var space2 = spaceBuffer.Dequeue();
			if (space2)
				spaceCount++;
			else
				notSpaceCount++;
		}
	}

	private bool TryProcessLZ(NList<uint> context, int curPos)
	{
		if (!preOutputBuffer.IsFull)
			return false;
		LZEntry bestValue = new(-1, -1);
		var contextIndex = contextSet.IndexOf(context);
		var indexes = lzBuffer.IndexesOf(contextIndex);
		indexes.Sort();
		foreach (var pos in indexes)
			ValidateLZBetterValue(curPos, ref bestValue, pos);
		if (bestValue.Pos == -1)
		{
			WriteNotLZCount();
			return false;
		}
		Result.Add(new(notLZCount, lzCount, lzCount + notLZCount));
		lzCount++;
		WriteLZPosition(curPos, bestValue.Pos);
		WriteLZLength(bestValue.Length);
		ClearBuffers();
		lzBlockEnd = curPos + bestValue.Length;
		return true;
	}

	private void ValidateLZBetterValue(int curPos, ref LZEntry bestValue, int pos)
	{
		var dist = (pos - (curPos - startPos - maxContextDepth)) % LZDictionarySize + curPos - startPos - maxContextDepth;
		int length;
		for (length = -maxContextDepth; length < Input.Length - startPos - curPos && RedStarLinq.Equals(Input[curPos + length], Input[dist + maxContextDepth + startPos + length], (x, y) => x.Lower == y.Lower); length++)
			;
		if (curPos - (dist + maxContextDepth + startPos) >= 2 && length > bestValue.Length)
			bestValue = new(pos, length);
	}

	private void WriteNotLZCount()
	{
		if (preOutputBuffer.IsFull)
		{
			Result.Add(new(0, notLZCount, lzCount + notLZCount));
			notLZCount++;
		}
	}

	private void WriteLZPosition(int curPos, int bestPos)
	{
		var sum = lzPositions.GetLeftValuesSum((uint)bestPos, out var posFrequency);
		if (sum >= 0 && posFrequency != 0)
		{
			Result.Add(new((uint)sum, (uint)posFrequency, (uint)lzPositions.ValuesSum));
			lzPositions.Update((uint)bestPos, posFrequency + 100);
		}
		else
		{
			Result.Add(new((uint)lzPositions.GetLeftValuesSum(uint.MaxValue, out var escapeFrequency), (uint)escapeFrequency, (uint)lzPositions.ValuesSum));
			lzPositions.Update(uint.MaxValue, escapeFrequency + 100);
			Result.Add(new((uint)bestPos, (uint)Min(curPos - startPos - maxContextDepth, LZDictionarySize - 1)));
			lzPositions.Add((uint)bestPos, 100);
		}
	}

	private void WriteLZLength(int bestLength)
	{
		if (bestLength < lzLengths.Length - 1)
		{
			Result.Add(new((uint)lzLengths.GetLeftValuesSum(bestLength, out var frequency), (uint)frequency, (uint)lzLengths.ValuesSum));
			lzLengths.Increase(bestLength);
		}
		else
		{
			Result.Add(new((uint)(lzLengths.ValuesSum - lzLengths[^1]), (uint)lzLengths[^1], (uint)lzLengths.ValuesSum));
			lzLengths.Increase(lzLengths.Length - 1);
			foreach (var bit in EncodeFibonacci((uint)(bestLength - lzLengths.Length + 2)))
				Result.Add(new(bit ? 1u : 0, 2));
			new Chain(bestLength - lzLengths.Length + 1).ForEach(x => lzLengths.Insert(lzLengths.Length - 1, 1));
		}
	}

	private void ClearBuffers()
	{
		preOutputBuffer.Clear();
		spaceBuffer.Clear();
		if (BlockIndex != 2)
			foreach (var x in newItemsBuffer.Filter(x => x != uint.MaxValue))
				newItemsFreqTable.Add((x, 1));
		newItemsBuffer.Clear();
	}

	private void Increase()
	{
		IncreaseInSkipped();
		var successLength = reservedContext.Length;
		if (reservedContext.Length != 0)
		{
			currentContext.Replace(reservedContext);
			currentContext.RemoveAt(^1);
		}
		IncreaseInEncoded(successLength);
		IncreaseInGlobal(successLength);
	}

	private void IncreaseInSkipped()
	{
		for (; reservedContext.Length > 0 && contextSet.TryAdd(reservedContext.Copy(), out var index); reservedContext.RemoveAt(^1))
		{
			if (lzBufferIndex == -1)
				lzBufferIndex = index;
			contextFreqTableByLevel.SetOrAdd(index, [(item, 100)]);
		}
	}

	private void IncreaseInEncoded(int successLength)
	{
		for (; reservedContext.Length > 0 && contextSet.TryGetIndexOf(reservedContext, out var index); reservedContext.RemoveAt(^1), _ = reservedContext.Length == 0 ? null : currentContext.RemoveAt(^1))
		{
			if (lzBufferIndex == -1)
				lzBufferIndex = index;
			if (!contextFreqTableByLevel[index].TryGetValue(item, out var itemValue))
			{
				contextFreqTableByLevel[index].Add(item, 100);
				continue;
			}
			else if (reservedContext.Length == 1 || itemValue > 100)
			{
				contextFreqTableByLevel[index].Update(item, itemValue + (int)Max(Round((double)100 / (successLength - reservedContext.Length + 1)), 1));
				continue;
			}
			ComplexIncreaseInEncoded(index, itemValue);
		}
	}

	private void ComplexIncreaseInEncoded(int index, int itemValue)
	{
		var successIndex = contextSet.IndexOf(currentContext);
		if (!contextFreqTableByLevel[successIndex].TryGetValue(item, out var successValue))
			successValue = 100;
		var step = (double)GetFreqTableBase(contextFreqTableByLevel[index]) * successValue
			/ (contextFreqTableByLevel[index].ValuesSum + GetFreqTableBase(contextFreqTableByLevel[successIndex]) - successValue);
		contextFreqTableByLevel[index].Update(item, (int)(Max(Round(step), 1) + itemValue));
	}

	private void IncreaseInGlobal(int successLength)
	{
		if (globalFreqTable.TryGetValue(item, out var globalValue))
			globalFreqTable.Update(item, globalValue + (int)Max(Round((double)100 / (successLength + 1)), 1));
		else
			globalFreqTable.Add(item, 100);
	}

	private static uint GetFreqTableBase(SumSet<uint> freqTable) => (uint)(freqTable.ValuesSum + freqTable.Length * 100);
}

file record struct PredictionEntry(long Sum, int Frequency);

file record struct LZEntry(int Pos, int Length);

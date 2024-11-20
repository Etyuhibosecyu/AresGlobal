global using Corlib.NStar;
global using System;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static Corlib.NStar.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;
using System.Numerics;
using ILGPU;
using ILGPU.Runtime;

namespace UnsafeFunctions;

public static unsafe class Global
{
	public const int MillisecondsPerSecond = 1000;
	public const int ProgressBarStep = 10;
	public const int ProgressBarHGroups = 3, ProgressBarVGroups = 3, ProgressBarGroups = ProgressBarHGroups * ProgressBarVGroups;
	public static Thread[] Threads { get; set; } = new Thread[ProgressBarGroups];
	public static int Supertotal { get; set; }
	public static int SupertotalMaximum { get; set; }
	public static int Total { get; set; }
	public static int TotalMaximum { get; set; }
	public static int[] Subtotal { get; set; } = new int[ProgressBarGroups];
	public static int[] SubtotalMaximum { get; set; } = new int[ProgressBarGroups];
	public static int[] Current { get; set; } = new int[ProgressBarGroups];
	public static int[] CurrentMaximum { get; set; } = new int[ProgressBarGroups];
	public static int[] Status { get; set; } = new int[ProgressBarGroups];
	public static int[] StatusMaximum { get; set; } = new int[ProgressBarGroups];
	public static Interval HuffmanApplied { get; } = new(0, 10, 10);
	public static Interval LempelZivApplied { get; } = new(0, 11, 11);
	public static Interval PixelsApplied { get; } = new(0, 12, 12);
	public static Interval SpacesApplied { get; } = new(0, 13, 13);
	public static Interval WordsApplied { get; } = new(0, 14, 14);
	public static Interval LengthsApplied { get; } = new(0, 15, 15);
	public static Interval BWTApplied { get; } = new(0, 16, 16);
	public static Interval LWApplied { get; } = new(0, 17, 17);
	public static Interval LempelZivSubdivided { get; } = new(0, 20, 20);
	public static Interval HuffmanDummyApplied { get; } = new(0, 21, 21);
	public static Interval LempelZivDummyApplied { get; } = new(0, 22, 22);
	public static NList<ShortIntervalList> ByteIntervals { get; } = RedStarLinq.NFill(ValuesInByte, index => new ShortIntervalList() { new Interval((uint)index, ValuesInByte) });
	public static NList<ShortIntervalList> ByteIntervals2 { get; } = RedStarLinq.NFill(ValuesInByte, index => new ShortIntervalList() { new Interval((uint)index, 269) });
	public static NList<ShortIntervalList> ByteIntervalsPlus1 { get; } = RedStarLinq.NFill(ValuesInByte + 1, index => new ShortIntervalList() { new Interval((uint)index, ValuesInByte + 1) });
	public static uint[] FibonacciSequence { get; } = [1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597, 2584, 4181, 6765, 10946, 17711, 28657, 46368, 75025, 121393, 196418, 317811, 514229, 832040, 1346269, 2178309, 3524578, 5702887, 9227465, 14930352, 24157817, 39088169, 63245986, 102334155, 165580141, 267914296, 433494437, 701408733, 1134903170, 1836311903, 2971215073];

	public static Vector<byte>[] AsFastVectors(Span<byte> bytes)
	{
		fixed (byte* ptr = bytes)
			return new Span<Vector<byte>>(ptr, bytes.Length / Vector<byte>.Count).ToArray();
	}

	public static T AsStruct<T>(this Span<byte> bytes) where T : unmanaged
	{
		fixed (byte* ptr = bytes)
			return *(T*)ptr;
	}

	public static NList<int> BWTCompare<T>(this NList<T> buffer, int n, int tn) where T : unmanaged
	{
		NList<nint> list = [];
		fixed (T* ptr = buffer.AsSpan())
		{
			for (var i = 0; i < n; i++)
				list.Add((nint)(ptr + i));
			if (n >= 1000000)
				return BWTCompare<T, T>(list, n, tn).ToNList().Sort(x => 0xffffffff - (uint)x);
			return BWTCompare<T, ulong>(list, GetArrayLength(n, sizeof(ulong) / sizeof(T)), tn).ConvertAndJoin(x => new Chain(x * sizeof(ulong) / sizeof(T), sizeof(ulong) / sizeof(T))).ToNList().Sort(x => 0xffffffff - (uint)x);
		}
	}

	private static ListHashSet<int> BWTCompare<T, TInternal>(NList<nint> list, int n, int tn) where T : unmanaged where TInternal : unmanaged
	{
		if (list.Length <= 1)
			return [];
		var level = 0;
		var pos = 0;
		ListHashSet<int> result = [0];
		Stack<NList<nint>> listStack = new(2 * BitsCount((uint)n + 1));
		Stack<List<NGroup<nint, TInternal>>> groupsStack = new(listStack.Length);
		Stack<int> levelStack = new(listStack.Length);
		Stack<int> posStack = new(listStack.Length);
		List<NGroup<nint, TInternal>> groups;
		var firstList = list;
		listStack.Push(list);
		groupsStack.Push(groups = Group(x => *(TInternal*)x));
		levelStack.Push(0);
		posStack.Push(0);
		while (groups.Length < list.Length)
		{
			list = groups[0];
			if (list.AllEqual(x => *((T*)x + (x == firstList[0] ? sizeof(TInternal) / sizeof(T) * n : 0) - 1)))
				break;
			var oldLevel = level++;
			while (level < n && groups.Length == 1 && list.AllEqual(x => *((TInternal*)x + level)))
				level++;
			if (level >= n)
			{
				level = oldLevel;
				break;
			}
			result.Add(level);
			listStack.Push(list);
			groups = Group(x => *((TInternal*)x + level));
			(groups[CreateVar(groups.IndexOfMax(x => x.Length), out var maxIndex)], groups[^1]) = (groups[^1], groups[maxIndex]);
			groupsStack.Push(groups);
			levelStack.Push(level);
			posStack.Push(0);
		}
		while (listStack.TryPeek(out list) && groupsStack.TryPeek(out groups) && levelStack.TryPeek(out level) && posStack.TryPop(out pos))
		{
			if (groups.Length >= list.Length || ++pos >= groups.Length)
			{
				listStack.Pop().Dispose();
				groupsStack.Pop().ForEach(x => x?.Dispose());
				levelStack.Pop();
				continue;
			}
			posStack.Push(pos);
			while (groups.Length < list.Length)
			{
				if (pos >= 1)
					groups[pos - 1].Dispose();
				list = groups[pos];
				if (list.AllEqual(x => *((T*)x + (x == firstList[0] ? sizeof(TInternal) / sizeof(T) * n : 0) - 1)))
					break;
				var oldLevel = level++;
				while (level < n && pos == groups.Length - 1 && list.AllEqual(x => *((TInternal*)x + level)))
					level++;
				if (level >= n)
				{
					level = oldLevel;
					break;
				}
				result.Add(level);
				if (pos == groups.Length - 1)
				{
					listStack.Pop();
					groupsStack.Pop();
					levelStack.Pop();
					posStack.Pop();
				}
				listStack.Push(list);
				groups = Group(x => *((TInternal*)x + level));
				(groups[CreateVar(groups.IndexOfMax(x => x.Length), out var maxIndex)], groups[^1]) = (groups[^1], groups[maxIndex]);
				groupsStack.Push(groups);
				levelStack.Push(level);
				posStack.Push(pos = 0);
			}
		}
		return result;
		List<NGroup<nint, TInternal>> Group(Func<nint, TInternal> innerFunc) => tn >= 0 && list.Length >= 1000 ? list.PNGroup(innerFunc) : list.NGroup(innerFunc);
	}

	public static BitList EncodeEqual(uint lower, uint @base)
	{
		if (lower >= @base)
			throw new InvalidOperationException();
		var bitsCount = BitsCount(@base);
		var threshold = (uint)(1 << bitsCount) - @base;
		var shifted = lower < threshold ? lower : (lower - threshold >> 1) + threshold;
		var result = new BitList(bitsCount - 1, shifted);
		result.Reverse();
		if (lower >= threshold)
			result.Add((lower - threshold & 1) != 0);
		return result;
	}

	public static BitList EncodeFibonacci(uint number)
	{
		BitList bits = default!;
		int i;
		for (i = FibonacciSequence.Length - 1; i >= 0; i--)
		{
			if (FibonacciSequence[i] <= number)
			{
				bits = new(i + 2, false) { [i] = true, [i + 1] = true };
				number -= FibonacciSequence[i];
				break;
			}
		}
		for (i--; i >= 0;)
		{
			if (FibonacciSequence[i] <= number)
			{
				bits[i] = true;
				number -= FibonacciSequence[i];
				i -= 2;
			}
			else
			{
				i--;
			}
		}
		return bits;
	}

	public static uint GetBaseWithBuffer(uint oldBase, bool words = false) => oldBase + GetBufferInterval(oldBase, words);
	public static uint GetBufferInterval(uint oldBase, bool words = false) => Max(words ? (oldBase + 10) / 20 : (uint)Sqrt(oldBase), 1);

	/// <summary>Считает количество бит в числе. Логарифм для этой цели использовать невыгодно, так как это достаточно медленная операция.</summary>
	/// <param name="x">Исходное число.</param>
	/// <returns>Количество бит в числе.</returns>
	public static int BitsCount(uint x)
	{
		var count = 0;
		while (x > 0)
		{
			x >>= 1;
			count++;
		}
		return count;
	}

	public static NList<Interval> GetCountList(uint count, uint maxT = 31)
	{
		NList<Interval> list = [];
		list.WriteCount(count, maxT);
		return list;
	}

	public static int GetSize<T>(this T _) where T : unmanaged => sizeof(T);

	public static List<NGroup<T, TKey>> PNGroup<T, TKey>(this G.IReadOnlyList<T> source, Func<T, TKey> function) where T : unmanaged where TKey : unmanaged
	{
		var lockObj = RedStarLinq.FillArray(Environment.ProcessorCount, x => new object());
		var length = source.Count;
		var innerIndexes = (int*)Marshal.AllocHGlobal(sizeof(int) * length);
		FillMemory(innerIndexes, length, 0);
		ParallelHashSet<TKey> hs = [];
		Parallel.For(0, length, i => hs.TryAdd(function(source[i]), out innerIndexes[i]));
		var dicKeys = hs.ToArray();
		var innerCount = (int*)Marshal.AllocHGlobal(sizeof(int) * hs.Length);
		FillMemory(innerCount, hs.Length, 0);
		var innerIndexes2 = (int*)Marshal.AllocHGlobal(sizeof(int) * length);
		FillMemory(innerIndexes2, length, 0);
		Parallel.For(0, length, i =>
		{
			int c;
			lock (lockObj[innerIndexes[i] % lockObj.Length])
				c = innerCount[innerIndexes[i]]++;
			innerIndexes2[i] = c;
		});
		var result = RedStarLinq.EmptyList<NGroup<T, TKey>>(hs.Length);
		Parallel.For(0, hs.Length, i => (result[i] = new NGroup<T, TKey>(innerCount[i], dicKeys[i])).Resize(innerCount[i]));
		Parallel.For(0, length, i => result[innerIndexes[i]][innerIndexes2[i]] = source[i]);
		Marshal.FreeHGlobal((nint)innerCount);
		Marshal.FreeHGlobal((nint)innerIndexes2);
		Marshal.FreeHGlobal((nint)innerIndexes);
		return result;
	}

	public static List<(NList<uint> Group, T Key)> PGroup<T>(this G.IReadOnlyList<T> source, int tn, G.IEqualityComparer<T>? comparer = null)
	{
		var lockObj = RedStarLinq.FillArray(Environment.ProcessorCount, x => new object());
		var length = source.Count;
		var innerIndexes = (int*)Marshal.AllocHGlobal(sizeof(int) * length);
		FillMemory(innerIndexes, length, 0);
		ParallelHashSet<T> hs = new(comparer);
		Status[tn] = 0;
		StatusMaximum[tn] = length;
		Parallel.For(0, length, i =>
		{
			hs.TryAdd(source[i], out innerIndexes[i]);
			Status[tn]++;
		});
		var dicKeys = hs.ToArray();
		var innerCount = (int*)Marshal.AllocHGlobal(sizeof(int) * hs.Length);
		FillMemory(innerCount, hs.Length, 0);
		var innerIndexes2 = (int*)Marshal.AllocHGlobal(sizeof(int) * length);
		FillMemory(innerIndexes2, length, 0);
		Parallel.For(0, length, i =>
		{
			int c;
			lock (lockObj[innerIndexes[i] % lockObj.Length])
				c = innerCount[innerIndexes[i]]++;
			innerIndexes2[i] = c;
		});
		var result = RedStarLinq.EmptyList<(NList<uint> Group, T Key)>(hs.Length);
		Parallel.For(0, hs.Length, i => result[i] = (RedStarLinq.NEmptyList<uint>(innerCount[i]), dicKeys[i]));
		Parallel.For(0, length, i => result[innerIndexes[i]].Group[innerIndexes2[i]] = (uint)i);
		Marshal.FreeHGlobal((nint)innerCount);
		Marshal.FreeHGlobal((nint)innerIndexes2);
		Marshal.FreeHGlobal((nint)innerIndexes);
		return result;
	}

	public static Span<byte> ReleaseFastVectors(Span<Vector<byte>> vectors)
	{
		fixed (Vector<byte>* ptr = vectors)
			return new Span<byte>(ptr, vectors.Length * Vector<byte>.Count);
	}

	public static Span<byte> StructAsSpan<T>(in T value) where T : unmanaged
	{
		fixed (T* ptr = &value)
			return new(ptr, sizeof(T));
	}

	public static void WriteCount(this NList<Interval> result, uint count, uint maxT = 31)
	{
		var t = Max(BitsCount(count) - 1, 0);
		result.Add(new((uint)t, maxT));
		var t2 = (uint)1 << Max(t, 1);
		result.Add(new(count - ((t == 0) ? 0 : t2), t2));
	}

	public static void WriteCount(this ShortIntervalList result, uint count, uint maxT = 31)
	{
		var t = Max(BitsCount(count) - 1, 0);
		result.Add(new((uint)t, maxT));
		var t2 = (uint)1 << Max(t, 1);
		result.Add(new(count - ((t == 0) ? 0 : t2), t2));
	}
}

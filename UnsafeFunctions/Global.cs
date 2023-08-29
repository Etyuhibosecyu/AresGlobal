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

namespace UnsafeFunctions;

public static unsafe class Global
{
	public const int MillisecondsPerSecond = 1000;
	public const int ProgressBarStep = 10;
	public const int BitsPerByte = 8;
	public const int ValuesInByte = 1 << BitsPerByte;
	public const int ValuesIn2Bytes = ValuesInByte << BitsPerByte;
	public const int ValuesIn3Bytes = ValuesIn2Bytes << BitsPerByte;
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
	public static Interval RepeatsApplied { get; } = new(224, 225);
	public static Interval RepeatsNotApplied { get; } = new(0, 224, 225);
	public static Interval LempelZivSubdivided { get; } = new(0, 20, 20);
	public static Interval HuffmanDummyApplied { get; } = new(0, 21, 21);
	public static Interval LempelZivDummyApplied { get; } = new(0, 22, 22);
	public static List<ShortIntervalList> ByteIntervals { get; } = RedStarLinq.Fill(ValuesInByte, index => new ShortIntervalList() { new Interval((uint)index, ValuesInByte) });
	public static List<ShortIntervalList> ByteIntervals2 { get; } = RedStarLinq.Fill(ValuesInByte, index => new ShortIntervalList() { new Interval((uint)index, 269) });
	public static uint[] FibonacciSequence { get; } = new uint[] { 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597, 2584, 4181, 6765, 10946, 17711, 28657, 46368, 75025, 121393, 196418, 317811, 514229, 832040, 1346269, 2178309, 3524578, 5702887, 9227465, 14930352, 24157817, 39088169, 63245986, 102334155, 165580141, 267914296, 433494437, 701408733, 1134903170, 1836311903, 2971215073 };

	public class IntListComparer : G.IComparer<List<int>>
	{
		public int Compare(List<int>? x, List<int>? y)
		{
			if (x == null && y == null)
				return 0;
			else if (x == null)
				return 1;
			else if (y == null)
				return -1;
			var n = Min(x.Length, y.Length);
			for (var i = 0; i < n; i++)
			{
				if (x[i] > y[i])
					return 1;
				else if (x[i] < y[i])
					return -1;
			}
			if (x.Length > y.Length)
				return 1;
			else if (x.Length < y.Length)
				return -1;
			return 0;
		}
	}

	public static List<(uint[] Group, TSource Key)> PGroup<TSource>(this G.IList<TSource> source, int tn, G.IEqualityComparer<TSource>? comparer = null)
	{
		var lockObj = RedStarLinq.FillArray(Environment.ProcessorCount, x => new object());
		var lockObj2 = RedStarLinq.FillArray(Environment.ProcessorCount, x => new object());
		var count = source.Count;
		var innerIndexes = (int*)Marshal.AllocHGlobal(sizeof(int) * count);
		FillMemory(innerIndexes, count, 0);
		ParallelHashSet<TSource> hs = new(comparer);
		Status[tn] = 0;
		StatusMaximum[tn] = count;
		Parallel.For(0, count, i =>
		{
			hs.TryAdd(source[i], out innerIndexes[i]);
			Status[tn]++;
		});
		var dicKeys = hs.ToArray();
		var innerCount = (int*)Marshal.AllocHGlobal(sizeof(int) * hs.Length);
		FillMemory(innerCount, hs.Length, 0);
		var innerIndexes2 = (int*)Marshal.AllocHGlobal(sizeof(int) * count);
		FillMemory(innerIndexes2, count, 0);
		Parallel.For(0, count, i =>
		{
			int c;
			lock (lockObj[innerIndexes[i] % lockObj.Length])
				c = innerCount[innerIndexes[i]]++;
			innerIndexes2[i] = c;
		});
		var result = RedStarLinq.EmptyList<(uint[] Group, TSource Key)>(hs.Length);
		Parallel.For(0, hs.Length, i => result[i] = (new uint[innerCount[i]], dicKeys[i]));
		Parallel.For(0, count, i => result[innerIndexes[i]].Group[innerIndexes2[i]] = (uint)i);
		Marshal.FreeHGlobal((IntPtr)innerCount);
		Marshal.FreeHGlobal((IntPtr)innerIndexes2);
		Marshal.FreeHGlobal((IntPtr)innerIndexes);
		return result;
	}

	public static List<(uint[] Group, TSource Key)> PGroup<TSource>(this NList<TSource> source, int tn, G.IEqualityComparer<TSource>? comparer = null) where TSource : unmanaged
	{
		var lockObj = RedStarLinq.FillArray(Environment.ProcessorCount, x => new object());
		var count = source.Length;
		var innerIndexes = (int*)Marshal.AllocHGlobal(sizeof(int) * count);
		FillMemory(innerIndexes, count, 0);
		ParallelHashSet<TSource> hs = new(comparer);
		Status[tn] = 0;
		StatusMaximum[tn] = count;
		Parallel.For(0, count, i =>
		{
			hs.TryAdd(source[i], out innerIndexes[i]);
			Status[tn]++;
		});
		var dicKeys = hs.ToArray();
		var innerCount = (int*)Marshal.AllocHGlobal(sizeof(int) * hs.Length);
		FillMemory(innerCount, hs.Length, 0);
		var innerIndexes2 = (int*)Marshal.AllocHGlobal(sizeof(int) * count);
		FillMemory(innerIndexes2, count, 0);
		Parallel.For(0, count, i =>
		{
			int c;
			lock (lockObj[innerIndexes[i] % lockObj.Length])
				c = innerCount[innerIndexes[i]]++;
			innerIndexes2[i] = c;
		});
		var result = RedStarLinq.EmptyList<(uint[] Group, TSource Key)>(hs.Length);
		Parallel.For(0, hs.Length, i => result[i] = (new uint[innerCount[i]], dicKeys[i]));
		Parallel.For(0, count, i => result[innerIndexes[i]].Group[innerIndexes2[i]] = (uint)i);
		Marshal.FreeHGlobal((IntPtr)innerCount);
		Marshal.FreeHGlobal((IntPtr)innerIndexes2);
		Marshal.FreeHGlobal((IntPtr)innerIndexes);
		return result;
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

	public static void WriteCount(this List<Interval> result, uint count, uint maxT = 31)
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

	public static List<Interval> GetCountList(uint count, uint maxT = 31)
	{
		List<Interval> list = new();
		list.WriteCount(count, maxT);
		return list;
	}

	public static uint GetBaseWithBuffer(uint oldBase) => oldBase + GetBufferInterval(oldBase);
	public static uint GetBufferInterval(uint oldBase) => Max((oldBase + 10) / 20, 1);

	/// <summary>Считает количество бит в числе. Логарифм для этой цели использовать невыгодно, так как это достаточно медленная операция.</summary>
	/// <param name="x">Исходное число.</param>
	/// <returns>Количество бит в числе.</returns>
	public static int BitsCount(uint x)
	{
		var x_ = x;
		var count = 0;
		while (x_ > 0)
		{
			x_ >>= 1;
			count++;
		}
		return count;
	}
}

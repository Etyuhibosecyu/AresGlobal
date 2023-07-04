
namespace UnsafeFunctions;

public static unsafe class BWTSortClass<T> where T : unmanaged, IComparable<T>
{

	private const int SortRadixMsdULongThreshold = 256;
	private const int Log2ofPowerOfTwoRadix = 8;

	public static void BWTSort(T[] arrayToBeSorted, int[] indexes, int bound = -1)
	{
		var maxLength = indexes.Length - 1;
		var type = GetPrimitiveType<T>();
		if (bound < 0)
		{
			var tempBound = arrayToBeSorted.Max();
			bound = ToInt(tempBound, type);
			bound += 2 - bound % 2;
		}
		RadixSortMsdULongInner(arrayToBeSorted, indexes, 0, indexes.Length, 0, maxLength, bound, type);
	}

	private static void RadixSortMsdULongInner(T[] inArray, int[] indexes, int first, int length, int index, int maxLength, int bound, PrimitiveType type)
	{
		var last = first + length - 1;
		if (length < SortRadixMsdULongThreshold)
		{
			if (length > 1)
				BWTSortClass2<T>.BWTSort(inArray, indexes, first, last);
			return;
		}
		var startOfBin = (int*)Marshal.AllocHGlobal(sizeof(int) * (bound + 1));
		var endOfBin = (int*)Marshal.AllocHGlobal(sizeof(int) * bound);
		var nextBin = 1;
		int temp;
	l0:
		var count = HistogramOneByteComponent(inArray, indexes, first, last, index, maxLength, bound, type);
		startOfBin[0] = endOfBin[0] = first; startOfBin[bound] = -1;         // sentinal
		for (var i = 1; i < bound; i++)
			startOfBin[i] = endOfBin[i] = startOfBin[i - 1] + count[i - 1];
		var bucketsUsed = 0;
		for (var i = 0; i < bound; i++)
			if (count[i] > 0)
				bucketsUsed++;
		Marshal.FreeHGlobal((nint)count);
		if (bucketsUsed <= 1)
		{
			if (index < maxLength)
			{
				index++;
				goto l0;
			}
			else
				goto l1;
		}
		for (var current = first; current <= last;)
		{
			int digit;
			var currentElement = indexes[current];  // get the compiler to recognize that a register can be used for the loop instead of a[_current] memory location
			while (endOfBin[digit = ToInt(inArray[currentElement + index], type)] != current)
			{
				temp = indexes[endOfBin[digit]];
				indexes[endOfBin[digit]++] = currentElement;
				currentElement = temp;
			}
			indexes[current] = currentElement;
			endOfBin[digit]++;
			while (endOfBin[nextBin - 1] == startOfBin[nextBin])
				nextBin++;   // skip over empty and full bins, when the end of the current bin reaches the start of the next bin
			current = endOfBin[nextBin - 1];
		}
		if (index < maxLength)          // end recursion when all the bits have been processes
		{
			index++;
			for (var i = 0; i < bound; i++)
				RadixSortMsdULongInner(inArray, indexes, startOfBin[i], endOfBin[i] - startOfBin[i], index, maxLength, bound, type);
		}
	l1:
		Marshal.FreeHGlobal((nint)startOfBin);
		Marshal.FreeHGlobal((nint)endOfBin);
	}

	private static int* HistogramOneByteComponent(T[] inArray, int[] indexes, int l, int r, int index, int maxLength, int bound, PrimitiveType type)
	{
		var count = (int*)Marshal.AllocHGlobal(sizeof(int) * bound);
		FillMemory(count, bound, 0);
		for (var current = l; current <= r; current++)
			count[maxLength >= index ? ToInt(inArray[indexes[current] + index], type) : 0]++;
		return count;
	}
}

public class BWTSortClass2<T> where T : unmanaged, IComparable<T>
{
	public static void BWTSort(T[] inArray, int[] indexes) => BWTSort(inArray, indexes, 0, indexes.Length - 1);

	public static void BWTSort(T[] inArray, int[] indexes, int lower, int upper)
	{
		if (upper - lower + 1 <= 0)
			return;
		int i, j;
		int lb, ub;
		int[] lbstack = new int[64], ubstack = new int[64];
		var stackpos = 0;
		int middle;
		Span<T> pivot;
		int temp;
		lbstack[0] = lower;
		ubstack[0] = upper;
		do
		{
			lb = lbstack[stackpos];
			ub = ubstack[stackpos];
			stackpos--;
			do
			{
				middle = (lb + ub) / 2;
				i = lb;
				j = ub;
				pivot = inArray.AsSpan(indexes[middle], indexes.Length);
				do
				{
					while (CompareSpans(inArray.AsSpan(indexes[i], indexes.Length), pivot) == -1)
						i++;
					while (CompareSpans(inArray.AsSpan(indexes[j], indexes.Length), pivot) == 1)
						j--;
					if (i <= j)
					{
						temp = indexes[i];
						indexes[i] = indexes[j];
						indexes[j] = temp;
						i++;
						j--;
					}
				} while (i <= j);
				if (i < middle)
				{
					if (i < ub)
					{
						stackpos++;
						lbstack[stackpos] = i;
						ubstack[stackpos] = ub;
					}
					ub = j;
				}
				else
				{
					if (j > lb)
					{
						stackpos++;
						lbstack[stackpos] = lb;
						ubstack[stackpos] = j;
					}
					lb = i;
				}
			} while (lb < ub);
		} while (stackpos >= 0);
	}

	/// <summary>Метод сравнения двух байтовых массивов</summary>
	/// <param name="left">Первый массив. Если <see langword="null"/>, то будет исключение</param>
	/// <param name="right">Второй массив. Если <see langword="null"/> или длина меньше чем первого массива, то будет исключение</param>
	/// <returns>Результат сравнения двух массивов: 1 - первый массив больше, 0 - массивы равны, -1 - первый массив меньше</returns>
	/// <remarks>В методе побайтно сравниваются два массива. 
	/// Если очередные байты не равны, то возвращается результат: 1 если байт первого массива больше, -1 если меньше.
	/// Если все байты оказались равны - возвращается 0.</remarks>
	private static int CompareSpans(Span<T> left, Span<T> right)
	{
		for (var i = 0; i < left.Length; i++)
		{
			if (left[i].CompareTo(right[i]) > 0)
				return 1;
			if (left[i].CompareTo(right[i]) < 0)
				return -1;
		}
		return 0;
	}
}

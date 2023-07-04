﻿using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace AresGlobalMethods;

[DebuggerDisplay("Length = {Length}")]
public class ArithmeticEncoder : IDisposable
{
	private readonly BitList bits = new(256);
	private const uint l0 = 0, h0 = uint.MaxValue, firstQtr = (h0 - 1) / 4 + 1, half = firstQtr * 2, thirdQtr = firstQtr * 3;
	private uint l = l0, h = h0;
	private int bitsToFollow;
	private readonly List<List<List<uint>>> bMaps = new();
	private readonly List<List<int>> deletedMaps = new();
	private readonly List<int> mapPtr = new();
	private readonly List<List<List<int>>> deletedMapElements = new();
	private readonly List<List<int>> mapElementPtr = new();
	private static readonly BitList[] shortLists = RedStarLinq.Fill(10, index => new BitList(index + 1, false)).Concat(RedStarLinq.Fill(10, index => new BitList(index + 1, true))).ToArray();

	public int Length => bits.Length;

	public void Dispose()
	{
		bits.Dispose();
		GC.SuppressFinalize(this);
	}

	public void Write(int c, int bMapsGroup, int mapIndex)
	{
		uint ol = l, oh = h;
		l = (uint)(ol + ((c == 0) ? 0 : bMaps[bMapsGroup][mapIndex][c - 1]) * ((ulong)oh - ol + 1) / bMaps[bMapsGroup][mapIndex][^1]);
		h = (uint)(ol + bMaps[bMapsGroup][mapIndex][c] * ((ulong)oh - ol + 1) / bMaps[bMapsGroup][mapIndex][^1] - 1);
		WriteInternal();
	}

	public void WriteEqual(uint c, uint cCount)
	{
		if (c < 0 || c >= cCount)
			throw new ArgumentException(null);
		uint ol = l, oh = h;
		l = (uint)(ol + c * ((ulong)oh - ol + 1) / cCount);
		h = (uint)(ol + (c + 1) * ((ulong)oh - ol + 1) / cCount - 1);
		WriteInternal();
	}

	public void WritePart(uint c, uint length, uint cCount)
	{
		if (c < 0 || c + length > cCount)
			throw new ArgumentException(null);
		uint ol = l, oh = h;
		l = (uint)(ol + c * ((ulong)oh - ol + 1) / cCount);
		h = (uint)(ol + (c + length) * ((ulong)oh - ol + 1) / cCount - 1);
		WriteInternal();
	}

	public void WriteFibonacci(uint number)
	{
		if (number == 0)
		{
			throw new ArgumentException(null);
		}
		var bits = EncodeFibonacci(number);
		for (var i = 0; i < bits.Length; i++)
		{
			WriteEqual((uint)(bits[i] ? 1 : 0), 2);
		}
	}

	private void WriteInternal()
	{
		while (true)
		{
			if (h < half)
			{
				Follow(false);
			}
			else if (l >= half)
			{
				Follow(true);
				l -= half;
				h -= half;
			}
			else if (l >= firstQtr && h < thirdQtr)
			{
				bitsToFollow++;
				l -= firstQtr;
				h -= firstQtr;
			}
			else
			{
				break;
			}
			l += l;
			h += h + 1;
		}
	}

	public void Append(ArithmeticEncoder ar)
	{
		if (ar.bits.Length != 0)
		{
			Follow(ar.bits[0]);
			bits.ReplaceRange(bits.Length - 1, 1, ar.bits);
		}
	}

	private void Follow(bool bit)
	{
		bits.Add(bit);
		if (bitsToFollow != 0)
			bits.AddRange(bitsToFollow <= 10 ? shortLists[(bit ? 0 : 10) + bitsToFollow - 1] : new(bitsToFollow, !bit));
		bitsToFollow = 0;
	}

	public int AddBMapsGroup()
	{
		bMaps.Add(new List<List<uint>>());
		deletedMaps.Add(new List<int>());
		mapPtr.Add(0);
		deletedMapElements.Add(new List<List<int>>());
		mapElementPtr.Add(new List<int>());
		return bMaps.Length - 1;
	}

	public void RemoveBMapsGroup(int index)
	{
		bMaps.RemoveAt(index);
		deletedMaps.RemoveAt(index);
		deletedMapElements.RemoveAt(index);
	}

	public int AddBMap(int bMapsGroup, List<uint> bMap)
	{
		if (mapPtr[bMapsGroup] >= deletedMaps[bMapsGroup].Length)
		{
			bMaps[bMapsGroup].Add(bMap);
			deletedMapElements[bMapsGroup].Add(new List<int>());
			mapElementPtr[bMapsGroup].Add(0);
			return bMaps[bMapsGroup].Length - 1;
		}
		else
		{
			bMaps[bMapsGroup][deletedMaps[bMapsGroup][mapPtr[bMapsGroup]]] = bMap;
			if (mapPtr[bMapsGroup] >= 500)
			{
				deletedMaps[bMapsGroup].Remove(0, 500);
				mapPtr[bMapsGroup] -= 500;
			}
			return deletedMaps[bMapsGroup][mapPtr[bMapsGroup]++];
		}
	}

	public void RemoveBMap(int bMapsGroup, int index)
	{
		bMaps[bMapsGroup][index] = new List<uint>();
		deletedMaps[bMapsGroup].Add(index);
		deletedMapElements[bMapsGroup][index] = new List<int>();
		mapElementPtr[bMapsGroup][index] = 0;
	}

	public int AddBMapElement(int bMapsGroup, int mapIndex, uint length)
	{
		if (mapElementPtr[bMapsGroup][mapIndex] >= deletedMapElements[bMapsGroup][mapIndex].Length)
		{
			bMaps[bMapsGroup][mapIndex].Add(((bMaps[bMapsGroup][mapIndex].Length == 0) ? 0 : bMaps[bMapsGroup][mapIndex][^1]) + length);
			return bMaps[bMapsGroup][mapIndex].Length - 1;
		}
		else
		{
			ChangeBMapElement(bMapsGroup, mapIndex, deletedMapElements[bMapsGroup][mapIndex][mapElementPtr[bMapsGroup][mapIndex]], (int)length);
			if (mapElementPtr[bMapsGroup][mapIndex] >= 500)
			{
				deletedMapElements[bMapsGroup][mapIndex].Remove(0, 500);
				mapElementPtr[bMapsGroup][mapIndex] -= 500;
			}
			return deletedMapElements[bMapsGroup][mapIndex][mapElementPtr[bMapsGroup][mapIndex]++];
		}
	}

	public void RemoveBMapElement(int bMapsGroup, int mapIndex, int elementIndex)
	{
		var n = -(int)(bMaps[bMapsGroup][mapIndex][elementIndex] - ((elementIndex == 0) ? 0 : bMaps[bMapsGroup][mapIndex][elementIndex - 1]));
		ChangeBMapElement(bMapsGroup, mapIndex, elementIndex, n);
		deletedMapElements[bMapsGroup][mapIndex].Add(elementIndex);
	}

	public void ChangeBMapElement(int bMapsGroup, int mapIndex, int elementIndex, int n)
	{
		for (var i = elementIndex; i < bMaps[bMapsGroup][mapIndex].Length; i++)
			bMaps[bMapsGroup][mapIndex][i] = Max((i == 0) ? 1 : bMaps[bMapsGroup][mapIndex][i - 1], (uint)(bMaps[bMapsGroup][mapIndex][i] + n));
	}

	public uint GetBMapElementWidth(int bMapsGroup, int mapIndex, int elementIndex) => bMaps[bMapsGroup][mapIndex][elementIndex] - (elementIndex == 0 ? 0 : bMaps[bMapsGroup][mapIndex][elementIndex - 1]);

	public static implicit operator byte[](ArithmeticEncoder x)
	{
		var bytes = new byte[(x.bits.Length + 7) / 8];
		x.bits.CopyTo(bytes, 0);
		return bytes;
	}

	public static implicit operator List<byte>(ArithmeticEncoder x) => ((byte[])x).ToList();
}

[DebuggerDisplay("Length = {Length}")]
public class ArithmeticDecoder : IDisposable
{
	private readonly BitList bits;
	private const uint l0 = 0, h0 = uint.MaxValue, firstQtr = (h0 - 1) / 4 + 1, half = firstQtr * 2, thirdQtr = firstQtr * 3;
	private uint l = l0, h = h0, value;
	private int pos;
	private readonly List<List<List<uint>>> bMaps = new();
	private readonly List<List<int>> deletedMaps = new();
	private readonly List<int> mapPtr = new();
	private readonly List<List<List<int>>> deletedMapElements = new();
	private readonly List<List<int>> mapElementPtr = new();

	public int Length => bits.Length;

	public ArithmeticDecoder(byte[] byteList)
	{
		bits = new(byteList);
		value = ReverseBits(bits.GetSmallRange(0, 32));
		pos = Min(32, bits.Length);
	}

	public ArithmeticDecoder(List<byte> byteList)
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

	public int Read(int bMapsGroup, int mapIndex)
	{
		uint ol = l, oh = h, divisor = bMaps[bMapsGroup][mapIndex][^1];
		if (divisor == 0)
			return 0;
		int c;
		var freq = (uint)((((ulong)value - ol + 1) * divisor - 1) / ((ulong)oh - ol + 1));
		for (c = 0; bMaps[bMapsGroup][mapIndex][c] <= freq; c++) ;
		l = (uint)(ol + ((c == 0) ? 0 : bMaps[bMapsGroup][mapIndex][c - 1]) * ((ulong)oh - ol + 1) / divisor);
		h = (uint)(ol + bMaps[bMapsGroup][mapIndex][c] * ((ulong)oh - ol + 1) / divisor - 1);
		ReadInternal();
		return c;
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
			{
				return true;
			}
			else
			{
				if (input)
				{
					value += FibonacciSequence[sequencePos];
				}
				sequencePos++;
				one = input;
			}
		}
		value = 0;
		return false;
	}

	private void ReadInternal()
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

	public int AddBMapsGroup()
	{
		bMaps.Add(new List<List<uint>>());
		deletedMaps.Add(new List<int>());
		mapPtr.Add(0);
		deletedMapElements.Add(new List<List<int>>());
		mapElementPtr.Add(new List<int>());
		return bMaps.Length - 1;
	}

	public void RemoveBMapsGroup(int index)
	{
		bMaps.RemoveAt(index);
		deletedMaps.RemoveAt(index);
		deletedMapElements.RemoveAt(index);
	}

	public int AddBMap(int bMapsGroup, List<uint> bMap)
	{
		if (mapPtr[bMapsGroup] >= deletedMaps[bMapsGroup].Length)
		{
			bMaps[bMapsGroup].Add(bMap);
			deletedMapElements[bMapsGroup].Add(new List<int>());
			mapElementPtr[bMapsGroup].Add(0);
			return bMaps[bMapsGroup].Length - 1;
		}
		else
		{
			bMaps[bMapsGroup][deletedMaps[bMapsGroup][mapPtr[bMapsGroup]]] = bMap;
			if (mapPtr[bMapsGroup] >= 500)
			{
				deletedMaps[bMapsGroup].Remove(0, 500);
				mapPtr[bMapsGroup] -= 500;
			}
			return deletedMaps[bMapsGroup][mapPtr[bMapsGroup]++];
		}
	}

	public void RemoveBMap(int bMapsGroup, int index)
	{
		bMaps[bMapsGroup][index] = new List<uint>();
		deletedMaps[bMapsGroup].Add(index);
		deletedMapElements[bMapsGroup][index] = new List<int>();
		mapElementPtr[bMapsGroup][index] = 0;
	}

	public int AddBMapElement(int bMapsGroup, int mapIndex, uint length)
	{
		if (mapElementPtr[bMapsGroup][mapIndex] >= deletedMapElements[bMapsGroup][mapIndex].Length)
		{
			bMaps[bMapsGroup][mapIndex].Add(((bMaps[bMapsGroup][mapIndex].Length == 0) ? 0 : bMaps[bMapsGroup][mapIndex][^1]) + length);
			return bMaps[bMapsGroup][mapIndex].Length - 1;
		}
		else
		{
			ChangeBMapElement(bMapsGroup, mapIndex, deletedMapElements[bMapsGroup][mapIndex][mapElementPtr[bMapsGroup][mapIndex]], (int)length);
			if (mapElementPtr[bMapsGroup][mapIndex] >= 500)
			{
				deletedMapElements[bMapsGroup][mapIndex].Remove(0, 500);
				mapElementPtr[bMapsGroup][mapIndex] -= 500;
			}
			return deletedMapElements[bMapsGroup][mapIndex][mapElementPtr[bMapsGroup][mapIndex]++];
		}
	}

	public void RemoveBMapElement(int bMapsGroup, int mapIndex, int elementIndex)
	{
		var n = -(int)(bMaps[bMapsGroup][mapIndex][elementIndex] - ((elementIndex == 0) ? 0 : bMaps[bMapsGroup][mapIndex][elementIndex - 1]));
		ChangeBMapElement(bMapsGroup, mapIndex, elementIndex, n);
		deletedMapElements[bMapsGroup][mapIndex].Add(elementIndex);
	}

	public void ChangeBMapElement(int bMapsGroup, int mapIndex, int elementIndex, int n)
	{
		for (var i = elementIndex; i < bMaps[bMapsGroup][mapIndex].Length; i++)
			bMaps[bMapsGroup][mapIndex][i] = Max((i == 0) ? 1 : bMaps[bMapsGroup][mapIndex][i - 1], (uint)(bMaps[bMapsGroup][mapIndex][i] + n));
	}

	public static implicit operator ArithmeticDecoder(byte[] x) => new(x);

	public static implicit operator ArithmeticDecoder(List<byte> x) => new(x);
}

public struct ImageData
{
	public int Width { get; private set; }
	public int Height { get; private set; }
	public int RAlpha { get; private set; }

	public ImageData(int width, int height, int rAlpha)
	{
		Width = width;
		Height = height;
		RAlpha = rAlpha;
	}

	public readonly void Deconstruct(out int Width, out int Height, out int RAlpha)
	{
		Width = this.Width;
		Height = this.Height;
		RAlpha = this.RAlpha;
	}

	public static implicit operator ImageData((int Width, int Height, int RAlpha) value) => new(value.Width, value.Height, value.RAlpha);
}

public struct HuffmanData
{
	public int MaxFrequency { get; private set; }
	public int FrequencyCount { get; private set; }
	public List<uint> ArithmeticMap { get; private set; }
	public List<Interval> UniqueList { get; private set; }
	public bool SpaceCodes { get; private set; }

	public HuffmanData(int maxFrequency, int frequencyCount, List<uint> arithmeticMap, List<Interval> uniqueList, bool spaceCodes = false)
	{
		MaxFrequency = maxFrequency;
		FrequencyCount = frequencyCount;
		ArithmeticMap = arithmeticMap;
		UniqueList = uniqueList;
		SpaceCodes = spaceCodes;
	}

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

public struct MethodDataUnit
{
	public uint R { get; private set; }
	public uint Max { get; private set; }
	public uint Threshold { get; private set; }

	public MethodDataUnit(uint r, uint max, uint threshold)
	{
		R = r;
		Max = max;
		Threshold = threshold;
	}

	public readonly void Deconstruct(out uint R, out uint Max, out uint Threshold)
	{
		R = this.R;
		Max = this.Max;
		Threshold = this.Threshold;
	}

	public static implicit operator MethodDataUnit((uint R, uint Max, uint Threshold) value) => new(value.R, value.Max, value.Threshold);
}

public struct LZData
{
	public MethodDataUnit Dist { get; private set; }
	public MethodDataUnit Length { get; private set; }
	public uint UseSpiralLengths { get; private set; }
	public MethodDataUnit SpiralLength { get; private set; }

	public LZData(MethodDataUnit dist, MethodDataUnit length, uint useSpiralLengths, MethodDataUnit spiralLength)
	{
		Dist = dist;
		Length = length;
		UseSpiralLengths = useSpiralLengths;
		SpiralLength = spiralLength;
	}

	public readonly void Deconstruct(out MethodDataUnit Dist, out MethodDataUnit Length, out uint UseSpiralLengths, out MethodDataUnit SpiralLength)
	{
		Dist = this.Dist;
		Length = this.Length;
		UseSpiralLengths = this.UseSpiralLengths;
		SpiralLength = this.SpiralLength;
	}

	public static implicit operator LZData((MethodDataUnit Dist, MethodDataUnit Length, uint UseSpiralLengths, MethodDataUnit SpiralLength) value) => new(value.Dist, value.Length, value.UseSpiralLengths, value.SpiralLength);
}

public class ArchaicHuffmanNode : G.IEnumerable<uint>
{
	public ArchaicHuffmanNode? Left { get; init; }
	public ArchaicHuffmanNode? Right { get; init; }
	public uint Item { get; init; }
	public int Count { get; init; }
	public bool IsLeaf => Left == null && Right == null;

	public ArchaicHuffmanNode(uint item, int frequency)
	{
		Left = null;
		Right = null;
		Item = item;
		Count = frequency;
	}

	public ArchaicHuffmanNode(ArchaicHuffmanNode first, ArchaicHuffmanNode second)
	{
		if (first.Count >= second.Count)
		{
			Left = first;
			Right = second;
		}
		else
		{
			Left = second;
			Right = first;
		}
		Item = 0;
		Count = first.Count + second.Count;
	}

	public G.IEnumerator<uint> GetEnumerator()
	{
		Stack<ArchaicHuffmanNode> stack = new();
		ArchaicHuffmanNode? current = this;
		while (current != null)
		{
			stack.Push(current);
			current = current.Left;
		}
		while (stack.Length != 0)
		{
			current = stack.Pop();
			if (current.IsLeaf)
				yield return current.Item;
			var node = current.Right;
			while (node != null)
			{
				stack.Push(node);
				node = node.Left;
			}
		}
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

[DebuggerDisplay("({String}, {Space})")]
public class Word
{
	public string String { get; private set; }
	public bool Space { get; private set; }

	public Word()
	{
		String = "";
		Space = false;
	}

	public Word(string string_, bool space)
	{
		String = string_;
		Space = space;
	}

	public override bool Equals(object? obj)
	{
		if (obj == null)
			return false;
		if (obj is not Word m)
			return false;
		return String == m.String;
	}

	public override int GetHashCode() => String.GetHashCode() ^ Space.GetHashCode();

	public static bool operator ==(Word x, Word y) => y is not null && x.String == y.String && x.Space == y.Space;

	public static bool operator !=(Word x, Word y) => !(x == y);
}

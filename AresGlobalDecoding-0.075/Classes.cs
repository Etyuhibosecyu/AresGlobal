using System.Collections;
using System.Diagnostics;

namespace AresGlobalMethods;

[DebuggerDisplay("Length = {Length}")]
public class ArithmeticEncoder : IDisposable
{
	private readonly BitList bits = new(256);
	private const uint l0 = 0, h0 = uint.MaxValue, firstQtr = (h0 - 1) / 4 + 1, half = firstQtr * 2, thirdQtr = firstQtr * 3;
	private uint l = l0, h = h0;
	private int bitsToFollow;
	private static readonly BitList[] shortLists = [.. RedStarLinq.Fill(10, index => new BitList(index + 1, false)), .. RedStarLinq.Fill(10, index => new BitList(index + 1, true))];

	public int Length => bits.Length;

	public void Dispose()
	{
		bits.Dispose();
		GC.SuppressFinalize(this);
	}

	public void Write(int c, NList<uint> map)
	{
		uint ol = l, oh = h;
		l = (uint)(ol + ((c == 0) ? 0 : map[c - 1]) * ((ulong)oh - ol + 1) / map[^1]);
		h = (uint)(ol + map[c] * ((ulong)oh - ol + 1) / map[^1] - 1);
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
			throw new ArgumentException(null);
		var bits = EncodeFibonacci(number);
		for (var i = 0; i < bits.Length; i++)
			WriteEqual((uint)(bits[i] ? 1 : 0), 2);
	}

	private void WriteInternal()
	{
		while (true)
		{
			if (h < half)
				Follow(false);
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

	private void Follow(bool bit)
	{
		bits.Add(bit);
		if (bitsToFollow != 0)
			bits.AddRange(bitsToFollow <= 10 ? shortLists[(bit ? 0 : 10) + bitsToFollow - 1] : new(bitsToFollow, !bit));
		bitsToFollow = 0;
	}

	public static implicit operator byte[](ArithmeticEncoder x)
	{
		var bytes = new byte[(x.bits.Length + 7) / 8];
		x.bits.CopyTo(bytes, 0);
		return bytes;
	}
}

public record struct ImageData(int Width, int Height, int RAlpha)
{
	public readonly void Deconstruct(out int Width, out int Height, out int RAlpha)
	{
		Width = this.Width;
		Height = this.Height;
		RAlpha = this.RAlpha;
	}

	public static implicit operator ImageData((int Width, int Height, int RAlpha) value) => new(value.Width, value.Height, value.RAlpha);
}

public record struct HuffmanData(int MaxFrequency, int FrequencyCount, NList<uint> ArithmeticMap, List<Interval> UniqueList, bool SpaceCodes = false);

public record struct MethodDataUnit(uint R, uint Max, uint Threshold);

public record struct LZData(MethodDataUnit Dist, MethodDataUnit Length, uint UseSpiralLengths, MethodDataUnit SpiralLength);

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
		var current = this;
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
public record struct Word(string String, bool Space);

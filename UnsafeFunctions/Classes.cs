using System.Collections;
using System.Diagnostics;

namespace UnsafeFunctions;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
[DebuggerDisplay("({Lower}, {Length} : {Base})")]
public readonly struct Interval : IEquatable<Interval>
{
	public uint Lower { get; init; }
	public uint Length { get; init; }
	public uint Base { get; init; }

	public Interval(uint lower, uint @base)
	{
		if (@base == 0)
			throw new ArgumentException(null, nameof(@base));
		if (lower < 0 || lower + 1 > @base)
			throw new ArgumentException(null);
		Lower = lower;
		Length = 1;
		Base = @base;
	}

	public Interval(uint lower, uint length, uint @base)
	{
		if (@base == 0)
			throw new ArgumentException(null, nameof(@base));
		if (lower < 0 || lower + length > @base)
			throw new ArgumentException(null);
		Lower = lower;
		Length = length;
		Base = @base;
	}

	public Interval(Interval other) : this(other.Lower, other.Length, other.Base)
	{
	}

	public override bool Equals(object? obj)
	{
		if (obj == null)
			return false;
		if (obj is not Interval m)
			return false;
		return Lower == m.Lower && Length == m.Length && Base == m.Base;
	}

	bool IEquatable<Interval>.Equals(Interval obj) => Equals(obj);

	public override int GetHashCode() => Lower.GetHashCode() ^ Length.GetHashCode() ^ Base.GetHashCode();

	public static bool operator ==(Interval x, Interval y) => x.Lower == y.Lower && x.Length == y.Length && x.Base == y.Base;

	public static bool operator !=(Interval x, Interval y) => !(x == y);
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
[DebuggerDisplay("Length = {Length}")]
public unsafe class ShortIntervalList : IDisposable, IList<Interval>
{
	private const int partSize = 4;
	private readonly Interval Item1 = default!, Item2 = default!, Item3 = default!, Item4 = default!;
	private Interval* secondPart = null;
	private byte _size;

	public ShortIntervalList()
	{
	}

	public ShortIntervalList(G.IEnumerable<Interval> collection)
	{
		foreach (var item in collection)
			Add(item);
	}

	public bool IsReadOnly => false;

	public int Length => _size;

	public Interval this[int index]
	{
		get => index >= 0 && index < _size ? GetInternal(index) : throw new ArgumentOutOfRangeException(nameof(index));
		set
		{
			if (index < 0 || index >= _size)
				throw new ArgumentOutOfRangeException(nameof(index));
			SetInternal(index, value);
		}
	}

	Interval G.IList<Interval>.this[int index] { get => this[index]; set => this[index] = value; }

	public void Add(Interval item) => SetInternal(_size++, item);

	void G.ICollection<Interval>.Add(Interval item) => Add(item);

	public void Clear() => _size = 0;

	public bool Contains(Interval item)
	{
		fixed (Interval* ptr = &Item1)
			for (var i = 0; i < _size && i < partSize; i++)
				if (ptr[i] == item)
					return true;
		for (var i = 0; i < _size - partSize && i < partSize; i++)
			if (secondPart[i] == item)
				return true;
		return false;
	}

	bool G.ICollection<Interval>.Contains(Interval item) => Contains(item);

	public void CopyTo(Interval[] array, int arrayIndex)
	{
		fixed (Interval* ptr = &Item1)
			for (var i = 0; i < _size && i < partSize; i++)
				array[arrayIndex++] = ptr[i];
		for (var i = 0; i < _size - partSize && i < partSize; i++)
			array[arrayIndex++] = secondPart[i];
	}

	void G.ICollection<Interval>.CopyTo(Interval[] array, int arrayIndex) => CopyTo(array, arrayIndex);

	public unsafe void Dispose()
	{
		Marshal.FreeHGlobal((nint)secondPart);
		_size = 0;
		GC.SuppressFinalize(this);
	}

	public Enumerator GetEnumerator() => new(this);

	G.IEnumerator<Interval> G.IEnumerable<Interval>.GetEnumerator() => GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	private Interval GetInternal(int index)
	{
		fixed (Interval* ptr = &Item1)
			return index switch
			{
				< partSize => ptr[index],
				< partSize * 2 => secondPart[index - partSize],
				_ => throw new ArgumentOutOfRangeException(nameof(index)),
			};
	}

	public int IndexOf(Interval item)
	{
		fixed (Interval* ptr = &Item1)
			for (var i = 0; i < _size && i < partSize; i++)
				if (ptr[i] == item)
					return i;
		for (var i = 0; i < _size - partSize && i < partSize; i++)
			if (secondPart[i] == item)
				return i + partSize;
		return -1;
	}

	int G.IList<Interval>.IndexOf(Interval item) => IndexOf(item);

	public void Insert(int index, Interval item)
	{
		if (index < 0 || index > _size)
			throw new ArgumentOutOfRangeException(nameof(index));
		if (_size >= partSize * 2)
			throw new InvalidOperationException();
		if (_size >= partSize && secondPart == null)
			secondPart = (Interval*)Marshal.AllocHGlobal(sizeof(Interval) * partSize);
		for (var i = _size - 1; i >= index; i--)
			SetInternal(i + 1, GetInternal(i));
		SetInternal(index, item);
		_size++;
	}

	void G.IList<Interval>.Insert(int index, Interval item) => Insert(index, item);

	public void RemoveAt(int index)
	{
		if (index < 0 || index >= _size)
			throw new ArgumentOutOfRangeException(nameof(index));
		for (var i = index; i < _size - 1; i++)
			SetInternal(i, GetInternal(i + 1));
		SetInternal(_size - 1, default!);
		_size--;
	}

	void G.IList<Interval>.RemoveAt(int index) => RemoveAt(index);

	public bool RemoveValue(Interval item)
	{
		var index = IndexOf(item);
		if (index >= 0)
			RemoveAt(index);
		return index >= 0;
	}

	private void SetInternal(int index, Interval value)
	{
		if (index >= partSize && secondPart == null)
			secondPart = (Interval*)Marshal.AllocHGlobal(sizeof(Interval) * partSize);
		fixed (Interval* ptr = &Item1)
			switch (index)
			{
				case < partSize: ptr[index] = value; break;
				case < partSize * 2: secondPart[index - partSize] = value; break;
				default: throw new ArgumentOutOfRangeException(nameof(index));
			}
	}

	public struct Enumerator : G.IEnumerator<Interval>
	{
		private readonly ShortIntervalList _list;
		private byte _index;

		public Enumerator(ShortIntervalList list) => _list = list;

		public Interval Current { get; private set; } = default!;

		readonly object IEnumerator.Current => Current;

		public readonly void Dispose()
		{
		}

		public bool MoveNext()
		{
			if (_index >= _list._size)
				return false;
			fixed (Interval* ptr = &_list.Item1)
				Current = _index switch
				{
					< partSize => ptr[_index],
					< partSize * 2 => _list.secondPart[_index - partSize],
					_ => default!,
				};
			_index++;
			return true;
		}

		public void Reset()
		{
			Current = default!;
			_index = 0;
		}
	}
}

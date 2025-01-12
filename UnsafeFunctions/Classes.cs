using System.Collections;
using System.Diagnostics;

namespace UnsafeFunctions;

/// <summary>
/// Интервал для арифметического кодирования
/// (подробнее см. <a href="https://github.com/Etyuhibosecyu/AresTools">здесь</a>, ниже списка файлов).
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
[DebuggerDisplay("({Lower}, {Length} : {Base})")]
public readonly struct Interval : IEquatable<Interval>
{
	/// <summary>См. <a href="https://github.com/Etyuhibosecyu/AresTools">здесь</a>, ниже списка файлов.</summary>
	public uint Lower { get; init; }
	/// <summary>См. <a href="https://github.com/Etyuhibosecyu/AresTools">здесь</a>, ниже списка файлов.</summary>
	public uint Length { get; init; }
	/// <summary>См. <a href="https://github.com/Etyuhibosecyu/AresTools">здесь</a>, ниже списка файлов.</summary>
	public uint Base { get; init; }

	/// <summary>Создает интервал только из нижней границы и основания, с длиной, равной 1.</summary>
	/// <exception cref="ArgumentException"></exception>
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

	/// <summary>Создает интервал из нижней границы, длины и основания.</summary>
	/// <exception cref="ArgumentException"></exception>
	public Interval(uint lower, uint length, uint @base)
	{
		if (@base == 0)
			throw new ArgumentException(null, nameof(@base));
		if (lower < 0 || lower + length > @base || length == 0)
			throw new ArgumentException(null);
		Lower = lower;
		Length = length;
		Base = @base;
	}

	/// <summary>Создает интервал из другого интервала.</summary>
	public Interval(Interval other) : this(other.Lower, other.Length, other.Base)
	{
	}

	/// <summary>Вырожденный интервал, никак не влияющий на вЫходной поток.</summary>
	public static Interval Default => new(0, 1);

	public override bool Equals(object? obj)
	{
		if (obj == null || obj is not Interval m)
			return false;
		return Lower == m.Lower && Length == m.Length && Base == m.Base;
	}

	bool IEquatable<Interval>.Equals(Interval obj) => Equals(obj);

	public override int GetHashCode() => Lower.GetHashCode() ^ Length.GetHashCode() ^ Base.GetHashCode();

	public static bool operator ==(Interval x, Interval y) => x.Lower == y.Lower && x.Length == y.Length && x.Base == y.Base;

	public static bool operator !=(Interval x, Interval y) => !(x == y);
}

/// <summary>
/// Короткий список интервалов, занимающий меньше памяти, чем полноценные
/// <see cref="List{T}">List</see>&lt;<see cref="Interval"/>&gt; или
/// <see cref="NList{T}">NList</see>&lt;<see cref="Interval"/>&gt;, но поддерживающий и намного меньше методов
/// (в качестве альтернативы можно использовать экстенты).
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
[DebuggerDisplay("Length = {Length}")]
public unsafe struct ShortIntervalList : IDisposable, IList<Interval>
{
	private const int partSize = 4;
	private static readonly int lengthOffset = sizeof(Interval) * partSize + sizeof(Interval*);
	private readonly Interval* items = (Interval*)Marshal.AllocHGlobal(lengthOffset + 1);

	/// <summary>Создает список нулевой длины.</summary>
	public ShortIntervalList() => FillMemory((byte*)items, lengthOffset + 1, 0);

	/// <summary>Создает список из последовательности <see cref="IEnumerable"/>&lt;<see cref="Interval"/>&gt;.</summary>
	public ShortIntervalList(G.IEnumerable<Interval> collection)
	{
		FillMemory((byte*)items, lengthOffset + 1, 0);
		foreach (var item in collection)
			Add(item);
	}

	public readonly bool IsReadOnly => false;

	public readonly int Length { get => *((byte*)items + lengthOffset); private set => *((byte*)items + lengthOffset) = value < ValuesInByte ? (byte)value : throw new InvalidOperationException(); }

	public readonly Interval this[int index]
	{
		get => index >= 0 && index < Length ? GetInternal(index) : throw new ArgumentOutOfRangeException(nameof(index));
		set
		{
			if (index < 0 || index >= Length)
				throw new ArgumentOutOfRangeException(nameof(index));
			SetInternal(index, value);
		}
	}

	/// <summary>
	/// Adds an item to the <see cref="G.ICollection{T}"/>.
	/// </summary>
	/// <param name="item">The object to add to the <see cref="G.ICollection{T}"/>.</param>
	public ShortIntervalList Add(Interval item)
	{
		SetInternal(Length++, item);
		return this;
	}

	void G.ICollection<Interval>.Add(Interval item) => Add(item);

	public void Clear() => Length = 0;

	public readonly bool Contains(Interval item)
	{
		for (var i = 0; i < Length && i < partSize; i++)
			if (items[i] == item)
				return true;
		var secondPart = *(Interval**)(items + partSize);
		for (var i = 0; i < Length - partSize && i < partSize; i++)
			if (secondPart[i] == item)
				return true;
		return false;
	}

	public readonly void CopyTo(Interval[] array, int arrayIndex)
	{
		for (var i = 0; i < Length && i < partSize; i++)
			array[arrayIndex++] = items[i];
		var secondPart = *(Interval**)(items + partSize);
		for (var i = 0; i < Length - partSize && i < partSize; i++)
			array[arrayIndex++] = secondPart[i];
	}

	public void Dispose()
	{
		if (items != null)
		{
			var secondPart = *(Interval**)(items + partSize);
			if (secondPart != null)
				Marshal.FreeHGlobal((nint)secondPart);
			Marshal.FreeHGlobal((nint)items);
			Length = 0;
		}
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Returns an enumerator that iterates through the collection.
	/// </summary>
	/// <returns>An enumerator that can be used to iterate through the collection.</returns>
	public readonly Enumerator GetEnumerator() => new(this);

	readonly G.IEnumerator<Interval> G.IEnumerable<Interval>.GetEnumerator() => GetEnumerator();

	readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	private readonly Interval GetInternal(int index)
	{
		var secondPart = *(Interval**)(items + partSize);
		return index switch
		{
			< partSize => items[index],
			< partSize * 2 => secondPart[index - partSize],
			_ => throw new ArgumentOutOfRangeException(nameof(index)),
		};
	}

	public readonly int IndexOf(Interval item)
	{
		for (var i = 0; i < Length && i < partSize; i++)
			if (items[i] == item)
				return i;
		var secondPart = *(Interval**)(items + partSize);
		for (var i = 0; i < Length - partSize && i < partSize; i++)
			if (secondPart[i] == item)
				return i + partSize;
		return -1;
	}

	public void Insert(int index, Interval item)
	{
		if (index < 0 || index > Length)
			throw new ArgumentOutOfRangeException(nameof(index));
		if (Length >= partSize * 2)
			throw new InvalidOperationException();
		var secondPart = *(Interval**)(items + partSize);
		if (Length >= partSize && secondPart == null)
			secondPart = *(Interval**)(items + partSize) = (Interval*)Marshal.AllocHGlobal(sizeof(Interval) * partSize);
		for (var i = Length - 1; i >= index; i--)
			SetInternal(i + 1, GetInternal(i));
		SetInternal(index, item);
		Length++;
	}

	/// <summary>
	/// Removes the <see cref="G.IList{T}"/> item at the specified index.
	/// </summary>
	/// <param name="index">The zero-based index of the item to remove.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public ShortIntervalList RemoveAt(int index)
	{
		if (index < 0 || index >= Length)
			throw new ArgumentOutOfRangeException(nameof(index));
		for (var i = index; i < Length - 1; i++)
			SetInternal(i, GetInternal(i + 1));
		SetInternal(Length - 1, default!);
		Length--;
		return this;
	}

	void G.IList<Interval>.RemoveAt(int index) => RemoveAt(index);

	/// <summary>
	/// Removes the <see cref="G.IList{T}"/> item by its value;
	/// </summary>
	/// <param name="item">The object to remove from the <see cref="G.ICollection{T}"/>.</param>
	public bool RemoveValue(Interval item)
	{
		var index = IndexOf(item);
		if (index >= 0)
			RemoveAt(index);
		return index >= 0;
	}

	/// <summary>
	/// Sets the element at the specified index.
	/// </summary>
	/// <param name="index">The zero-based index of the element to get or set.</param>
	/// <param name="item">The element to set at the specified index.</param>
	public readonly ShortIntervalList Set(Index index, Interval item)
	{
		this[index] = item;
		return this;
	}

	private readonly void SetInternal(int index, Interval value)
	{
		var secondPart = *(Interval**)(items + partSize);
		if (index >= partSize && secondPart == null)
			secondPart = *(Interval**)(items + partSize) = (Interval*)Marshal.AllocHGlobal(sizeof(Interval) * partSize);
		switch (index)
		{
			case < partSize: items[index] = value; break;
			case < partSize * 2: secondPart[index - partSize] = value; break;
			default: throw new ArgumentOutOfRangeException(nameof(index));
		}
	}

	/// <summary>
	/// Copies the elements of the <see cref="ShortIntervalList"/> to a new array.
	/// </summary>
	/// <returns>An array containing copies of the elements of the <see cref="ShortIntervalList"/>.</returns>
	public readonly Interval[] ToArray()
	{
		var array = new Interval[Length];
		CopyTo(array, 0);
		return array;
	}

	/// <summary>
	/// Copies the elements of the <see cref="ShortIntervalList"/> to a new array.
	/// </summary>
	/// <param name="list">An array containing copies of the elements of the <see cref="ShortIntervalList"/></param>
	public struct Enumerator(ShortIntervalList list) : G.IEnumerator<Interval>
	{
		private byte _index;

		public Interval Current { get; private set; } = default!;

		readonly object IEnumerator.Current => Current;

		public readonly void Dispose()
		{
		}

		public bool MoveNext()
		{
			if (_index >= list.Length)
				return false;
			var secondPart = *(Interval**)(list.items + partSize);
			Current = _index switch
			{
				< partSize => list.items[_index],
				< partSize * 2 => secondPart[_index - partSize],
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

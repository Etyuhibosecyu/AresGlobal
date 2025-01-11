
namespace AresGlobalMethods;

public abstract record class RLEBase(NList<byte> Input, int TN)
{
	private protected virtual void InitProgressBars(int tn)
	{
		Current[tn] = 0;
		CurrentMaximum[tn] = 0;
		Status[tn] = 0;
		StatusMaximum[tn] = Input.Length;
	}

	private protected virtual NList<byte> RepeatSerieMarker(int len)
	{
		if (len < ValuesInByte >> 1)
			return [(byte)(len - 1)];
		else
		{
			var len2 = len - (ValuesInByte >> 1);
			return [((ValuesInByte >> 1) - 1), (byte)(len2 >> BitsPerByte), unchecked((byte)len2)];
		}
	}

	private protected virtual NList<byte> NoRepeatSerieMarker(int len)
	{
		if (len + 1 < ValuesInByte >> 1)
			return [(byte)(len + (ValuesInByte >> 1))];
		else
		{
			var len2 = len + 1 - (ValuesInByte >> 1);
			return [(ValuesInByte - 1), (byte)(len2 >> BitsPerByte), unchecked((byte)len2)];
		}
	}

	public abstract NList<byte> Encode(bool updateStatus = true);
}

public record class RLE(NList<byte> Input, int TN) : RLEBase(Input, TN)
{
	public override NList<byte> Encode(bool updateStatus = true)
	{
		NList<byte> result = [];
		if (Input.Length < 1)
			return Input;
		InitProgressBars(TN);
		for (var i = 0; i < Input.Length;)
		{
			result.Add(Input[Status[TN] = i++]);
			if (i == Input.Length)
				break;
			var j = i;
			while (i < Input.Length && i - j < ValuesIn2Bytes && Input[i] == Input[i - 1])
				Status[TN] = i++;
			if (i != j)
			{
				result.AddRange(RepeatSerieMarker(i - j));
				continue;
			}
			j = i;
			while (i < Input.Length && i - j < ValuesIn2Bytes && Input[i] != Input[i - 1])
				Status[TN] = i++;
			i--;
			result.AddRange(NoRepeatSerieMarker(i - j)).AddRange(Input.GetSlice(j..i));
		}
#if DEBUG
		var decoded = new RLEDec(result).Decode();
		for (var i = 0; i < Input.Length && i < decoded.Length; i++)
		{
			var x = Input[i];
			var y = decoded[i];
			if (!x.Equals(y))
				throw new DecoderFallbackException();
		}
		if (Input.Length != decoded.Length)
			throw new DecoderFallbackException();
#endif
		return result;
	}

	public NList<byte> RLEN(int n)
	{
		if (Input.Length < n || Input.Length % n != 0)
			return Input;
		NList<byte> result = [];
		var length = Input.Length / n;
		Current[TN] = 0;
		CurrentMaximum[TN] = 0;
		Status[TN] = 0;
		StatusMaximum[TN] = length;
		for (var i = 0; i < length;)
		{
			result.AddRange(Input.GetSlice(i++ * n, n));
			if (i == length)
				break;
			var j = i;
			while (i < length && i - j < ValuesIn2Bytes && Input.Compare(i * n, Input, (i - 1) * n, n) == n)
				i++;
			if (i != j)
			{
				result.AddRange(RepeatSerieMarker(i - j));
				continue;
			}
			j = i;
			while (i < length && i - j < ValuesIn2Bytes && Input.Compare(i * n, Input, (i - 1) * n, n) != n)
				i++;
			i--;
			result.AddRange(NoRepeatSerieMarker(i - j)).AddRange(Input.GetSlice((j * n)..(i * n)));
		}
		return result;
	}
}

public record class RLE3(NList<byte> Input, int TN) : RLEBase(Input, TN)
{
	public override NList<byte> Encode(bool updateStatus = true)
	{
		NList<byte> result = [];
		if (Input.Length < 3 || Input.Length % 3 != 0)
			return Input;
		var length = Input.Length / 3;
		if (updateStatus)
		{
			Current[TN] = 0;
			CurrentMaximum[TN] = 0;
			Status[TN] = 0;
			StatusMaximum[TN] = length;
		}
		for (var i = 0; i < length;)
		{
			result.AddRange(Input.GetSlice(i++ * 3, 3));
			if (updateStatus)
				Status[TN]++;
			if (i == length)
				break;
			var j = i;
			while (IsRLE3Serie(length, i, j, false))
			{
				i++;
				if (updateStatus)
					Status[TN]++;
			}
			if (i != j)
			{
				result.AddRange(RepeatSerieMarker(i - j));
				continue;
			}
			j = i;
			while (IsRLE3Serie(length, i, j, true))
			{
				i++;
				if (updateStatus)
					Status[TN]++;
			}
			i--;
			result.AddRange(NoRepeatSerieMarker(i - j)).AddRange(Input.GetSlice((j * 3)..(i * 3)));
		}
#if DEBUG
		var decoded = new RLEDec(result).DecodeRLE3();
		for (var i = 0; i < Input.Length && i < decoded.Length; i++)
		{
			var x = Input[i];
			var y = decoded[i];
			if (!x.Equals(y))
				throw new DecoderFallbackException();
		}
		if (Input.Length != decoded.Length)
			throw new DecoderFallbackException();
#endif
		return result;
	}

	private bool IsRLE3Serie(int length, int i, int j, bool noRepeat) => i < length && i - j < ValuesIn2Bytes && Input.Compare(i * 3, Input, (i - 1) * 3, 3) == 3 ^ noRepeat;
}

public record class RLEMixed(NList<byte> Input, int TN) : RLEBase(Input, TN)
{
	public override NList<byte> Encode(bool updateStatus = true)
	{
		NList<byte> result = [];
		if (Input.Length < 1)
			return Input;
		InitProgressBars(TN);
		for (var i = 0; i < Input.Length;)
		{
			result.Add(Input[Status[TN] = i++]);
			if (i == Input.Length)
				break;
			var j = i;
			while (i < Input.Length && i - j < ValuesIn2Bytes && Input[i] == Input[i - 1])
				Status[TN] = i++;
			if (i >= j + 2)
			{
				result.AddRange(RepeatSerieMarker(i - j));
				continue;
			}
			i = j;
			while (i < Input.Length - 4 && i - j < ValuesIn2Bytes * 3 && Input.Compare(i + 2, Input, i - 1, 3) == 3)
				Status[TN] = (i += 3) - 1;
			if (i != j)
			{
				result.AddRange(RepeatSerieMarker3(i - j));
				result.Add(Input[i++]).Add(Input[i++]);
				continue;
			}
			i = j;
			while (IsRLEMixedAltering(Input, i, j))
				Status[TN] = i++;
			i--;
			result.AddRange(NoRepeatSerieMarker(i - j)).AddRange(Input.GetSlice(j..i));
		}
#if DEBUG
		var decoded = new RLEDec(result).DecodeMixed();
		for (var i = 0; i < Input.Length && i < decoded.Length; i++)
		{
			var x = Input[i];
			var y = decoded[i];
			if (!x.Equals(y))
				throw new DecoderFallbackException();
		}
		if (Input.Length != decoded.Length)
			throw new DecoderFallbackException();
#endif
		return result;
	}

	private static bool IsRLEMixedAltering(NList<byte> input, int i, int j)
	{
		if (i >= input.Length)
			return false;
		if (i - j >= ValuesIn2Bytes)
			return false;
		if (input[i] != input[i - 1] || i < input.Length - 1 && input[i] != input[i + 1])
			if (i == j || i >= input.Length - 5 || input.Compare(i + 3, input, i, 3) != 3)
				return true;
		return false;
	}

	private protected override NList<byte> RepeatSerieMarker(int len)
	{
		if (len < ValuesInByte >> 2)
			return [(byte)(len - 1)];
		else
		{
			var len2 = len - (ValuesInByte >> 2);
			return [((ValuesInByte >> 2) - 1), (byte)(len2 >> BitsPerByte), unchecked((byte)len2)];
		}
	}

	private static NList<byte> RepeatSerieMarker3(int len)
	{
		if (len / 3 < ValuesInByte >> 2)
			return [(byte)(len / 3 - 1 + (ValuesInByte >> 2))];
		else
		{
			var len2 = len / 3 - (ValuesInByte >> 2);
			return [((ValuesInByte >> 1) - 1), (byte)(len2 >> BitsPerByte), unchecked((byte)len2)];
		}
	}
}

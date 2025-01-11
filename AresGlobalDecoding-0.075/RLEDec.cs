
namespace AresGlobalMethods;

public record class RLEDec(NList<byte> ByteList)
{
	protected byte temp;
	protected int length, serie, l;

	public virtual NList<byte> Decode()
	{
		NList<byte> result = [];
		Status[0] = 0;
		StatusMaximum[0] = ByteList.Length;
		byte element;
		for (var i = 0; i < ByteList.Length;)
		{
			result.Add(element = ByteList[i++]);
			if (i >= ByteList.Length)
				break;
			if (ProcessSerieAndLength(ref i))
				break;
			if (serie == 1)
			{
				for (var j = 0; j < length; j++)
					result.Add(element);
			}
			else
			{
				l = Min(length - 1, ByteList.Length - i + 1);
				result.AddRange(ByteList.GetRange(i, l));
				i += l;
			}
			Status[0] = i;
		}
		return result;
	}

	public virtual NList<byte> DecodeRLE3()
	{
		NList<byte> result = [];
		Status[0] = 0;
		StatusMaximum[0] = ByteList.Length;
		NList<byte> element;
		for (var i = 0; i < ByteList.Length - 2;)
		{
			result.AddRange(element = ByteList.GetRange(i, 3));
			i += 3;
			if (i >= ByteList.Length)
				break;
			if (ProcessSerieAndLength(ref i))
				break;
			if (serie == 1)
			{
				for (var j = 0; j < length; j++)
					result.AddRange(element);
			}
			else
			{
				l = Min((length - 1) * 3, ByteList.Length - i + 3);
				result.AddRange(ByteList.GetRange(i, l));
				i += l;
			}
			Status[0] = i;
		}
		return result;
	}

	protected virtual bool ProcessSerieAndLength(ref int i)
	{
		temp = ByteList[i++];
		if (temp >= ValuesInByte >> 1)
			serie = 2;
		else
			serie = 1;
		if (temp % (ValuesInByte >> 1) != (ValuesInByte >> 1) - 1)
			length = temp % (ValuesInByte >> 1) + 1;
		else
		{
			if (i >= ByteList.Length - 1)
				return true;
			length = (ByteList[i++] << BitsPerByte) + ByteList[i++] + (ValuesInByte >> 1);
		}
		return false;
	}

	public virtual NList<byte> DecodeMixed()
	{
		NList<byte> result = [];
		Status[0] = 0;
		StatusMaximum[0] = ByteList.Length;
		byte element;
		for (var i = 0; i < ByteList.Length;)
		{
			result.Add(element = ByteList[i++]);
			if (i >= ByteList.Length)
				break;
			if (ProcessSerieAndLengthMixed(ref i))
				break;
			if (serie == 1)
			{
				for (var j = 0; j < length; j++)
					result.Add(element);
			}
			else if (serie == 3)
			{
				byte second = ByteList[i++], third = ByteList[i++];
				result.Add(second).Add(third);
				for (var j = 0; j < length; j++)
					result.Add(element).Add(second).Add(third);
			}
			else
			{
				l = Min(length - 1, ByteList.Length - i + 1);
				result.AddRange(ByteList.GetRange(i, l));
				i += l;
			}
			Status[0] = i;
		}
		return result;
	}

	protected virtual bool ProcessSerieAndLengthMixed(ref int i)
	{
		temp = ByteList[i++];
		var divisor = ValuesInByte >> 2;
		if (temp >= ValuesInByte >> 1)
		{
			serie = 2;
			divisor <<= 1;
		}
		else if (temp >= ValuesInByte >> 2)
			serie = 3;
		else
			serie = 1;
		if (temp % divisor != divisor - 1)
			length = temp % divisor + 1;
		else
		{
			if (i >= ByteList.Length - 1)
				return true;
			length = (ByteList[i++] << BitsPerByte) + ByteList[i++] + divisor;
		}
		return false;
	}
}

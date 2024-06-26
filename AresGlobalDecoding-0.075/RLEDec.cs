
namespace AresGlobalMethods;

public class RLEDec(NList<byte> byteList)
{
	protected byte temp;
	protected int length, serie, l;
	public virtual NList<byte> ByteList { get; } = byteList;

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
			if (i >= ByteList.Length - 2)
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
}

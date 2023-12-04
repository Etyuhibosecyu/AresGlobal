global using Corlib.NStar;
global using static System.Math;
global using static UnsafeFunctions.Global;

namespace AresGlobalMethods007;

public class RLEDec(NList<byte> byteList) : AresGlobalMethods005.RLEDec(byteList)
{
	public override NList<byte> Decode()
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

	public override NList<byte> DecodeRLE3()
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
}

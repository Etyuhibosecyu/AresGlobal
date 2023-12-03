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

namespace AresGlobalMethods;

public static class Decoding
{
	public static NList<byte> DecodeRLE3(this NList<byte> byteList)
	{
		NList<byte> result = [];
		Status[0] = 0;
		StatusMaximum[0] = byteList.Length;
		NList<byte> element;
		int length, serie, l;
		byte temp;
		for (var i = 0; i < byteList.Length - 2;)
		{
			result.AddRange(element = byteList.GetRange(i, 3));
			i += 3;
			if (i >= byteList.Length - 2)
				break;
			temp = byteList[i++];
			if (temp >= ValuesInByte >> 1)
				serie = 2;
			else
				serie = 1;
			if (temp % (ValuesInByte >> 1) != (ValuesInByte >> 1) - 1)
				length = temp % (ValuesInByte >> 1) + 1;
			else
			{
				if (i >= byteList.Length - 1)
					break;
				length = (byteList[i++] << BitsPerByte) + byteList[i++] + (ValuesInByte >> 1);
			}
			if (serie == 1)
			{
				for (var j = 0; j < length; j++)
					result.AddRange(element);
			}
			else
			{
				l = Min((length - 1) * 3, byteList.Length - i + 3);
				result.AddRange(byteList.GetRange(i, l));
				i += l;
			}
			Status[0] = i;
		}
		return result;
	}

	public static NList<byte> DecodeRLE(this NList<byte> byteList)
	{
		NList<byte> result = [];
		Status[0] = 0;
		StatusMaximum[0] = byteList.Length;
		byte element;
		int length, serie, l;
		byte temp;
		for (var i = 0; i < byteList.Length;)
		{
			result.Add(element = byteList[i++]);
			if (i >= byteList.Length)
				break;
			temp = byteList[i++];
			if (temp >= ValuesInByte >> 1)
				serie = 2;
			else
				serie = 1;
			if (temp % (ValuesInByte >> 1) != (ValuesInByte >> 1) - 1)
				length = temp % (ValuesInByte >> 1) + 1;
			else
			{
				if (i >= byteList.Length - 1)
					break;
				length = (byteList[i++] << BitsPerByte) + byteList[i++] + (ValuesInByte >> 1);
			}
			if (serie == 1)
			{
				for (var j = 0; j < length; j++)
					result.Add(element);
			}
			else
			{
				l = Min(length - 1, byteList.Length - i + 1);
				result.AddRange(byteList.GetRange(i, l));
				i += l;
			}
			Status[0] = i;
		}
		return result;
	}
}

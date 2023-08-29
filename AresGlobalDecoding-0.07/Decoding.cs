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

public class Decoding
{
	public static List<ShortIntervalList> DecodeLempelZiv(List<ShortIntervalList> compressedList, bool lz, int lzRDist, uint lzThresholdDist, int lzRLength, uint lzThresholdLength, uint lzUseSpiralLengths, int lzRSpiralLength, uint lzThresholdSpiralLength, int tn)
	{
		List<ShortIntervalList> result = new();
		if (!lz)
			return compressedList;
		Current[tn] = 0;
		CurrentMaximum[tn] = 0;
		Status[tn] = 0;
		StatusMaximum[tn] = compressedList.Length;
		uint dist, length, spiralLength;
		int j, j2;
		for (var i = 0; i < compressedList.Length; i++, Status[tn]++)
		{
			if (i >= 2 && compressedList[i][0].Lower + compressedList[i][0].Length == compressedList[i][0].Base)
			{
				var readSpiralLength = false;
				if (lzRLength == 0)
				{
					length = compressedList[i][1].Lower;
					j = 1;
				}
				else
				{
					if (lzRLength == 1 != (compressedList[i][1].Lower == compressedList[i][1].Base - 1))
					{
						length = compressedList[i][lzRLength].Lower;
						j = lzRLength;
					}
					else
					{
						length = (uint)(compressedList[i][3 - lzRLength].Lower + lzThresholdLength + 2 - lzRLength);
						j = 3 - lzRLength;
					}
				}
				if (lzUseSpiralLengths == 0)
				{
					if (lzRDist == 0 || result.Length - length - 2 < lzThresholdDist)
					{
						dist = compressedList[i][j + 1].Lower;
						j2 = j + 1;
					}
					else
					{
						if (lzRDist == 1 != (compressedList[i][j + 1].Lower == compressedList[i][j + 1].Base - 1))
						{
							dist = compressedList[i][j + lzRDist].Lower;
							j2 = j + lzRDist;
						}
						else
						{
							dist = (uint)(compressedList[i][j + 3 - lzRDist].Lower + lzThresholdDist + 2 - lzRDist);
							j2 = j + 3 - lzRDist;
						}
					}
				}
				else if (lzRDist == 0 || result.Length - length - 2 < lzThresholdDist)
				{
					if (compressedList[i][j + 1].Lower == compressedList[i][j + 1].Base - 1)
					{
						dist = 0;
						readSpiralLength = true;
					}
					else
						dist = compressedList[i][j + 1].Lower;
					j2 = j + 1;
				}
				else
				{
					if (lzRDist == 1 != (compressedList[i][j + 1].Lower == compressedList[i][j + 1].Base - 1))
					{
						if (lzRDist == 2 && compressedList[i][j + lzRDist].Lower == compressedList[i][j + lzRDist].Base - 1)
						{
							dist = 0;
							readSpiralLength = true;
						}
						else
							dist = compressedList[i][j + lzRDist].Lower;
						j2 = j + lzRDist;
					}
					else
					{
						if (3 - lzRDist == 2 && compressedList[i][j + 3 - lzRDist].Lower == compressedList[i][j + 3 - lzRDist].Base - 1)
						{
							dist = 0;
							readSpiralLength = true;
						}
						else
							dist = (uint)(compressedList[i][j + 3 - lzRDist].Lower + lzThresholdDist + 2 - lzRDist);
						j2 = j + 3 - lzRDist;
					}
				}
				if (readSpiralLength)
				{
					if (lzRSpiralLength == 0)
						spiralLength = compressedList[i][j2 + 1].Lower;
					else
					{
						if (lzRSpiralLength == 1 != (compressedList[i][j2 + 1].Lower == compressedList[i][j2 + 1].Base - 1))
							spiralLength = compressedList[i][j2 + lzRSpiralLength].Lower;
						else
							spiralLength = (uint)(compressedList[i][j2 + 3 - lzRSpiralLength].Lower + lzThresholdSpiralLength + 2 - lzRSpiralLength);
					}
				}
				else
					spiralLength = 0;
				var start = (int)(result.Length - dist - length - 2);
				if (start < 0)
					return result;
				for (var k = (int)((length + 2) * (spiralLength + 1)); k > 0; k -= (int)length + 2)
					result.AddRange(result.GetSlice(start, (int)Min(length + 2, k)));
			}
			else
				result.Add(compressedList[i]);
		}
		return result;
	}

	public static NList<byte> DecodeRLE3(NList<byte> byteList)
	{
		NList<byte> result = new();
		Status[0] = 0;
		StatusMaximum[0] = byteList.Length;
		NList<byte> element;
		int length, serie, l;
		byte temp;
		for (var i = 0; i < byteList.Length - 2;)
		{
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
			if (i >= byteList.Length - 2)
				break;
			element = byteList.GetRange(i, 3);
			i += 3;
			if (serie == 1)
			{
				for (var j = 0; j < length; j++)
					result.AddRange(element);
			}
			else
			{
				l = Min((length - 1) * 3, byteList.Length - i + 3);
				result.AddRange(byteList.GetRange(i - 3, l));
				i += l - 3;
			}
			Status[0] = i;
		}
		return result;
	}

	public static NList<byte> DecodeRLE(NList<byte> byteList)
	{
		NList<byte> result = new();
		Status[0] = 0;
		StatusMaximum[0] = byteList.Length;
		byte element;
		int length, serie, l;
		byte temp;
		for (var i = 0; i < byteList.Length;)
		{
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
			if (i >= byteList.Length)
				break;
			element = byteList[i++];
			if (serie == 1)
			{
				for (var j = 0; j < length; j++)
					result.Add(element);
			}
			else
			{
				l = Min(length - 1, byteList.Length - i + 1);
				result.AddRange(byteList.GetRange(i - 1, l));
				i += l - 1;
			}
			Status[0] = i;
		}
		return result;
	}
}

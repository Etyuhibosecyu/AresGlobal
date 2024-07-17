
namespace AresGlobalMethods;

public static class DecodingExtents
{
	public const int WordsListActualParts = 3;
	public static int BWTBlockSize { get; set; } = 50000;
#pragma warning disable CS0652 // Сравнение с константой интеграции бесполезно: константа находится за пределами диапазона типа
	public static int BWTBlockExtraSize => BWTBlockSize <= 0x4000 ? 2 : BWTBlockSize <= 0x400000 ? 3 : BWTBlockSize <= 0x40000000 ? 4 : BWTBlockSize <= 0x4000000000 ? 5 : BWTBlockSize <= 0x400000000000 ? 6 : BWTBlockSize <= 0x40000000000000 ? 7 : 8;
#pragma warning restore CS0652 // Сравнение с константой интеграции бесполезно: константа находится за пределами диапазона типа
	public static int FragmentLength { get; set; } = 16000000;
	public static int PreservedFragmentLength { get; set; } = FragmentLength;

	public static uint GetFragmentLength() => (uint)FragmentLength;

	public static List<ShortIntervalList> DecodeLempelZiv(this List<ShortIntervalList> compressedList, bool lz, int lzRDist, uint lzThresholdDist, int lzRLength, uint lzThresholdLength, uint lzUseSpiralLengths, int lzRSpiralLength, uint lzThresholdSpiralLength, int tn)
	{
		List<ShortIntervalList> result = [];
		if (!lz)
			return compressedList;
		Status[tn] = 0;
		StatusMaximum[tn] = compressedList.Length;
		uint dist, length, spiralLength;
		int j, j2;
		for (var i = 0; i < compressedList.Length; i++, Status[tn]++)
		{
			if (i < 2 || compressedList[i][0].Lower + compressedList[i][0].Length != compressedList[i][0].Base)
			{
				result.Add(compressedList[i]);
				continue;
			}
			var readSpiralLength = false;
			if (lzRLength == 0)
			{
				length = compressedList[i][1].Lower;
				j = 1;
			}
			else if (lzRLength == 1 != (compressedList[i][1].Lower == compressedList[i][1].Base - 1))
			{
				length = compressedList[i][lzRLength].Lower;
				j = lzRLength;
			}
			else
			{
				length = (uint)(compressedList[i][3 - lzRLength].Lower + lzThresholdLength + 2 - lzRLength);
				j = 3 - lzRLength;
			}
			if (lzUseSpiralLengths == 0)
			{
				if (lzRDist == 0 || result.Length - length - 2 < lzThresholdDist)
				{
					dist = compressedList[i][j + 1].Lower;
					j2 = j + 1;
				}
				else if (lzRDist == 1 != (compressedList[i][j + 1].Lower == compressedList[i][j + 1].Base - 1))
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
			else if (lzRDist == 1 != (compressedList[i][j + 1].Lower == compressedList[i][j + 1].Base - 1))
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
			if (readSpiralLength)
			{
				if (lzRSpiralLength == 0)
					spiralLength = compressedList[i][j2 + 1].Lower;
				else if (lzRSpiralLength == 1 != (compressedList[i][j2 + 1].Lower == compressedList[i][j2 + 1].Base - 1))
					spiralLength = compressedList[i][j2 + lzRSpiralLength].Lower;
				else
					spiralLength = (uint)(compressedList[i][j2 + 3 - lzRSpiralLength].Lower + lzThresholdSpiralLength + 2 - lzRSpiralLength);
			}
			else
				spiralLength = 0;
			var start = (int)(result.Length - dist - length - 2);
			if (start < 0)
				return result;
			for (var k = (int)((length + 2) * (spiralLength + 1)); k > 0; k -= (int)length + 2)
				result.AddRange(result.GetSlice(start, (int)Min(length + 2, k)));
		}
		return result;
	}

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

	public static uint ReadCount(this ArithmeticDecoder ar, uint maxT = 31)
	{
		var temp = (int)ar.ReadEqual(maxT);
		var read = ar.ReadEqual((uint)1 << Max(temp, 1));
		return read + ((temp == 0) ? 0 : (uint)1 << Max(temp, 1));
	}

	public static uint GetBaseWithBuffer(uint oldBase) => oldBase + GetBufferInterval(oldBase);
	public static uint GetBufferInterval(uint oldBase) => Max((oldBase + 10) / 20, 1);
}

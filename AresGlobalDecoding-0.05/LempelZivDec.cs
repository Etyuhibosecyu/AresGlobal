
namespace AresGlobalMethods005;

public class LempelZivDec(List<ShortIntervalList> compressedList, bool lz, LZData lzData, int tn)
{
	public List<ShortIntervalList> Decode()
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
			if (lzData.Length.R == 0)
			{
				j = 1;
				length = compressedList[i][j].Lower;
			}
			else if (lzData.Length.R == 1 != (compressedList[i][1].Lower == compressedList[i][1].Base - 1))
			{
				j = lzData.Length.R;
				length = compressedList[i][j].Lower;
			}
			else
			{
				j = 3 - lzData.Length.R;
				length = (uint)(compressedList[i][j].Lower + lzData.Length.Threshold + 2 - lzData.Length.R);
			}
			if (lzData.UseSpiralLengths == 0)
			{
				if (lzData.Dist.R == 0 || result.Length - length - 2 < lzData.Dist.Threshold)
				{
					j2 = j + 1;
					dist = compressedList[i][j2].Lower;
				}
				else if (lzData.Dist.R == 1 != (compressedList[i][j + 1].Lower == compressedList[i][j + 1].Base - 1))
				{
					j2 = j + lzData.Dist.R;
					dist = compressedList[i][j2].Lower;
				}
				else
				{
					j2 = j + 3 - lzData.Dist.R;
					dist = (uint)(compressedList[i][j2].Lower + lzData.Dist.Threshold + 2 - lzData.Dist.R);
				}
			}
			else if (lzData.Dist.R == 0 || result.Length - length - 2 < lzData.Dist.Threshold)
			{
				j2 = j + 1;
				if (compressedList[i][j2].Lower == compressedList[i][j2].Base - 1)
				{
					dist = 0;
					readSpiralLength = true;
				}
				else
					dist = compressedList[i][j2].Lower;
			}
			else if (lzData.Dist.R == 1 != (compressedList[i][j + 1].Lower == compressedList[i][j + 1].Base - 1))
			{
				j2 = j + lzData.Dist.R;
				if (lzData.Dist.R == 2 && compressedList[i][j2].Lower == compressedList[i][j2].Base - 1)
				{
					dist = 0;
					readSpiralLength = true;
				}
				else
					dist = compressedList[i][j2].Lower;
			}
			else
			{
				j2 = j + 3 - lzData.Dist.R;
				if (3 - lzData.Dist.R == 2 && compressedList[i][j2].Lower == compressedList[i][j2].Base - 1)
				{
					dist = 0;
					readSpiralLength = true;
				}
				else
					dist = (uint)(compressedList[i][j2].Lower + lzData.Dist.Threshold + 2 - lzData.Dist.R);
			}
			if (readSpiralLength)
			{
				if (lzData.SpiralLength.R == 0)
					spiralLength = compressedList[i][j2 + 1].Lower;
				else if (lzData.SpiralLength.R == 1 != (compressedList[i][j2 + 1].Lower == compressedList[i][j2 + 1].Base - 1))
					spiralLength = compressedList[i][j2 + lzData.SpiralLength.R].Lower;
				else
					spiralLength = (uint)(compressedList[i][j2 + 3 - lzData.SpiralLength.R].Lower + lzData.SpiralLength.Threshold + 2 - lzData.SpiralLength.R);
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
}

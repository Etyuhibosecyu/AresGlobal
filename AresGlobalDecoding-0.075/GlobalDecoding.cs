global using Corlib.NStar;
global using System;
global using System.Runtime.InteropServices;
global using System.Text;
global using System.Threading;
global using System.Threading.Tasks;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static AresGlobalMethods.DecodingExtents;
global using static Corlib.NStar.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;

namespace AresGlobalMethods;

public class GlobalDecoding(ArithmeticDecoder ar)
{

	public virtual void ProcessLZLength(LZData lzData, out uint length)
	{
		if (lzData.Length.R == 0)
			length = ar.ReadEqual(lzData.Length.Max + 1);
		else if (lzData.Length.R == 1)
		{
			length = ar.ReadEqual(lzData.Length.Threshold + 2);
			if (length == lzData.Length.Threshold + 1)
				length += ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold);
		}
		else
		{
			length = ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold + 2) + lzData.Length.Threshold;
			if (length == lzData.Length.Max + 1)
				length = ar.ReadEqual(lzData.Length.Threshold);
		}
	}

	public virtual void ProcessLZLength(LZData lzData, SumList lengthsSL, out int readIndex, out uint length)
	{
		readIndex = ar.ReadPart(lengthsSL);
		lengthsSL.Increase(readIndex);
		if (lzData.Length.R == 0)
			length = (uint)readIndex;
		else if (lzData.Length.R == 1)
		{
			length = (uint)readIndex;
			if (length == lzData.Length.Threshold + 1)
				length += ar.ReadEqual(lzData.Length.Max - lzData.Length.Threshold);
		}
		else
		{
			length = (uint)readIndex + lzData.Length.Threshold;
			if (length == lzData.Length.Max + 1)
				length = ar.ReadEqual(lzData.Length.Threshold);
		}
	}

	public virtual void ProcessLZDist(LZData lzData, int fullLength, out uint dist, uint length, out uint maxDist)
	{
		maxDist = Min(lzData.Dist.Max, (uint)(fullLength - length - 2));
		if (lzData.Dist.R == 0 || maxDist < lzData.Dist.Threshold)
			dist = ar.ReadEqual(maxDist + lzData.UseSpiralLengths + 1);
		else if (lzData.Dist.R == 1)
		{
			dist = ar.ReadEqual(lzData.Dist.Threshold + 2);
			if (dist == lzData.Dist.Threshold + 1)
				dist += ar.ReadEqual(maxDist - lzData.Dist.Threshold + lzData.UseSpiralLengths);
		}
		else
		{
			dist = ar.ReadEqual(maxDist - lzData.Dist.Threshold + 2) + lzData.Dist.Threshold;
			if (dist == maxDist + 1)
			{
				dist = ar.ReadEqual(lzData.Dist.Threshold + lzData.UseSpiralLengths);
				if (dist == lzData.Dist.Threshold)
					dist = maxDist + 1;
			}
		}
	}

	public virtual void ProcessLZDist(LZData lzData, SumList distsSL, int fullLength, out int readIndex, out uint dist, uint length, out uint maxDist)
	{
		maxDist = Min(lzData.Dist.Max, (uint)(fullLength - length - 2));
		readIndex = ar.ReadPart(distsSL);
		distsSL.Increase(readIndex);
		if (lzData.Dist.R == 0 || maxDist < lzData.Dist.Threshold)
			dist = (uint)readIndex;
		else if (lzData.Dist.R == 1)
		{
			dist = (uint)readIndex;
			if (dist == lzData.Dist.Threshold + 1)
				dist += ar.ReadEqual(maxDist - lzData.Dist.Threshold + lzData.UseSpiralLengths);
		}
		else
			dist = (uint)readIndex;
	}

	public virtual bool ProcessLZSpiralLength(LZData lzData, ref uint dist, out uint spiralLength, uint maxDist)
	{
		if (dist == maxDist + 1)
		{
			if (lzData.SpiralLength.R == 0)
				spiralLength = ar.ReadEqual(lzData.SpiralLength.Max + 1);
			else if (lzData.SpiralLength.R == 1)
			{
				spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold + 2);
				if (spiralLength == lzData.SpiralLength.Threshold + 1)
					spiralLength += ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold);
			}
			else
			{
				spiralLength = ar.ReadEqual(lzData.SpiralLength.Max - lzData.SpiralLength.Threshold + 2) + lzData.SpiralLength.Threshold;
				if (spiralLength == lzData.SpiralLength.Max + 1)
					spiralLength = ar.ReadEqual(lzData.SpiralLength.Threshold);
			}
			return true;
		}
		spiralLength = 0;
		return false;
	}

	public virtual PPMDec CreatePPM(uint @base, int blockIndex = -1) => new(this, ar, @base, blockIndex);
}

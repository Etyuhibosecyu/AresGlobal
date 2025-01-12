global using AresGlobalMethods;
global using Corlib.NStar;
global using System;
global using System.IO;
global using System.Text;
global using System.Threading.Tasks;
global using UnsafeFunctions;
global using G = System.Collections.Generic;
global using static AresGlobalMethods.DecodingExtents;
global using static Corlib.NStar.Extents;
global using static System.Math;
global using static UnsafeFunctions.Global;
using System.Diagnostics;

namespace AresGlobalMethods;

public static class Global
{
	/// <summary>
	/// Преобразует двумерный список интервалов в байты
	/// (см. <a href="https://github.com/Etyuhibosecyu/AresTools">здесь</a>, ниже списка файлов, пункт "двойной список").
	/// </summary>
	/// <param name="tn">Номер потока (см. <see cref="Threads"/>).</param>
	public static NList<byte> WorkUpDoubleList(NList<ShortIntervalList> input, int tn)
	{
		if (input.Length == 0)
			return [];
		ArithmeticEncoder ar = new();
		ar.WriteCount((uint)input.Length);
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		Status[tn] = 0;
		StatusMaximum[tn] = input.Length;
		for (var i = 0; i < input.Length; i++)
		{
			for (var j = 0; j < input[i].Length; j++)
				ar.WritePart(input[i][j]);
			Status[tn]++;
		}
		ar.WriteEqual(1234567890, 4294967295);
		Current[tn] += ProgressBarStep;
		NList<byte> bytes = ar;
		ar.Dispose();
		return bytes;
	}

	/// <summary>
	/// Преобразует трехмерный список интервалов в байты
	/// (см. <a href="https://github.com/Etyuhibosecyu/AresTools">здесь</a>, ниже списка файлов, пункт "двойной список").
	/// </summary>
	/// <param name="tn">Номер потока (см. <see cref="Threads"/>).</param>
	public static NList<byte> WorkUpTripleList(List<NList<ShortIntervalList>> input, int tn)
	{
		if (input.Any(x => x.Length == 0))
			return [];
		ArithmeticEncoder ar = new();
		Current[tn] = 0;
		CurrentMaximum[tn] = ProgressBarStep * 2;
		Status[tn] = 0;
		StatusMaximum[tn] = input.Sum(x => x.Length);
		for (var i = 0; i < input.Length; i++)
		{
			ar.WriteCount((uint)input[i].Length);
			for (var j = 0; j < input[i].Length; j++)
			{
				for (var k = 0; k < input[i][j].Length; k++)
					ar.WritePart(input[i][j][k]);
				Status[tn]++;
			}
		}
		ar.WriteEqual(1234567890, 4294967295);
		Current[tn] += ProgressBarStep;
		NList<byte> bytes = ar;
		ar.Dispose();
		return bytes;
	}

	/// <summary>См. ReadNumber() в файле DecodingExtents.cs в проекте AresGlobalDecoding.</summary>
	public static void WriteCount(this ArithmeticEncoder ar, uint originalBase)
	{
		var t = Max(BitsCount(originalBase) - 1, 0);
		ar.WriteEqual((uint)t, 31);
		var t2 = (uint)1 << Max(t, 1);
		ar.WriteEqual(originalBase - ((t == 0) ? 0 : t2), t2);
	}
}

/// <summary>
/// Класс, выполняющий сжатие методом Хаффмана
/// (см. <a href="https://github.com/Etyuhibosecyu/AresTools">здесь</a>, ниже списка файлов).<br/>
/// Использование: <tt>new Huffman(input, result, TN).Encode();</tt>.<br/>
/// </summary>
/// <param name="Input">Входной поток для сжатия.</param>
/// <param name="Result">В<u>ы</u>ходной поток для сжатых данных.</param>
/// <param name="TN">Номер потока (см. <see cref="Threads"/>).</param>
public record class Huffman(NList<ShortIntervalList> Input, NList<ShortIntervalList> Result, int TN)
{
	private bool lz, lzDummy, spaces, consequentElems;
	private int bwtIndex, lzIndex, lzDummyIndex, bwtLength, lzHeaderLength, startPos, lzPos, maxFrequency;
	private NList<Interval> uniqueList = default!;
	private NList<int> indexCodes = default!, frequency = default!;
	private NList<(int elem, int freq)> frequencyTable = default!;

	/// <summary>Основной метод класса. Инструкция по применению - см. в описании класса.</summary>
	public NList<ShortIntervalList> Encode()
	{
		if (Input.Length == 0)
			throw new EncoderFallbackException();
		bwtIndex = Input[0].IndexOf(BWTApplied);
		if (CreateVar(Input[0].IndexOf(HuffmanApplied), out var huffmanIndex) != -1 && (bwtIndex == -1 || huffmanIndex != bwtIndex + 1))
			return Input;
		Prerequisites();
		if (Input.Length < startPos + 2)
			return Input;
		spaces = Input[0].Length >= 2 && Input[0][1] == SpacesApplied;
		Status[TN] = 0;
		StatusMaximum[TN] = 7;
		ProcessGroups(Input, TN);
		frequency = frequencyTable.PNConvert(x => x.freq);
		Status[TN]++;
		ProcessArithmeticMaps();
		Status[TN]++;
		NList<Interval> c = [];
		c.WriteNumber((uint)maxFrequency - 1);
		c.WriteNumber((uint)frequencyTable.Length - 1);
		Status[TN] = 0;
		StatusMaximum[TN] = frequencyTable.Length;
		Current[TN] += ProgressBarStep;
		if (consequentElems)
			for (var i = 0; i < frequencyTable.Length; i++, Status[TN]++)
			{
				c.Add(uniqueList[frequencyTable[i].elem]);
				if (i != 0)
					c.Add(new((uint)frequency[i] - 1, (uint)frequency[i - 1]));
			}
		else
			for (var i = 0; i < frequencyTable.Length; i++, Status[TN]++)
				c.Add(new(frequency[i] >= 1 ? (uint)frequency[i] - 1 : throw new EncoderFallbackException(), (uint)maxFrequency));
		uniqueList.Dispose();
		frequencyTable.Dispose();
		frequency.Dispose();
		var cSplit = c.SplitIntoEqual(8);
		c.Dispose();
		var cLength = (uint)cSplit.Length;
		var insertIndex = lz ? lzIndex : lzDummy ? lzDummyIndex : Result[0].Length;
		Result[0].Insert(insertIndex, HuffmanApplied);
		Result[0].Insert(insertIndex + 1, new(0, cLength, cLength));
		Result.Insert(startPos - bwtLength, cSplit.PConvert(x => new ShortIntervalList(x)));
		cSplit.Dispose();
		return Result;
	}

	private void Prerequisites()
	{
		Current[TN] = 0;
		CurrentMaximum[TN] = ProgressBarStep * 2;
		Result.Replace(Input);
		Result[0] = new(Result[0]);
		lz = (lzIndex = Input[0].IndexOf(LempelZivApplied)) != -1 && (bwtIndex == -1 || lzIndex != bwtIndex + 1);
		lzDummyIndex = Input[0].IndexOf(LempelZivDummyApplied);
		lzDummy = lzDummyIndex != -1 && (bwtIndex == -1 || lzDummyIndex != bwtIndex + 1);
		bwtLength = bwtIndex != -1 ? (int)Input[0][bwtIndex + 1].Base : 0;
		if (!lz && !lzDummy)
			lzHeaderLength = 1;
		else if (Input[0].Length >= lzIndex + 2 && Input[0][lzIndex + 1] == LempelZivSubdivided)
			lzHeaderLength = 3;
		else
			lzHeaderLength = 2;
		startPos = lzHeaderLength + (Input[0].Length >= 1 && Input[0][0] == LengthsApplied ? (int)Input[0][1].Base : 0) + bwtLength;
		lzPos = bwtIndex != -1 ? 4 : 2;
	}

	private void ProcessGroups(NList<ShortIntervalList> input, int tn)
	{
		using var indexedInput = input.GetRange(startPos).ToNList((x, index) => (elem: x[0], index));
		if (lz)
			indexedInput.FilterInPlace(x => x.index < 2 || x.elem.Lower + x.elem.Length != x.elem.Base);
		using var groups = indexedInput.Group(x => x.elem.Lower);
		maxFrequency = groups.Max(x => x.Length);
		consequentElems = maxFrequency > input[startPos][0].Base * 2 || input[startPos][0].Base <= ValuesInByte + 1;
		if (consequentElems)
			groups.NSort(x => 4294967295 - (uint)x.Length);
		Status[tn]++;
		uniqueList = groups.PNConvert(x => new Interval(x[0].elem) { Base = input[startPos][0].Base });
		Status[tn]++;
		indexCodes = RedStarLinq.NEmptyList<int>(input.Length - startPos);
		for (var i = 0; i < groups.Length; i++)
			foreach (var (_, index) in groups[i])
				indexCodes[index] = i;
		Status[tn]++;
		frequencyTable = groups.PNConvert((x, index) => (index, x.Length));
		Status[tn]++;
	}

	private void ProcessArithmeticMaps()
	{
		var intervalsBase = (uint)frequency.Sum();
		using var arithmeticMap = GetArithmeticMap(frequency);
		Status[TN]++;
		if (lz)
			intervalsBase = GetBaseWithBuffer(arithmeticMap[^1], spaces);
		using var frequencyIntervals = arithmeticMap.Prepend(0u).GetROLSlice(0, arithmeticMap.Length).ToNList((x, index) => new Interval(x, (uint)frequency[index], intervalsBase));
		Status[TN]++;
		Interval lzInterval = lz ? new(arithmeticMap[^1], intervalsBase - arithmeticMap[^1], intervalsBase) : new();
		Status[TN] = 0;
		StatusMaximum[TN] = Input.Length - startPos;
		Current[TN] += ProgressBarStep;
		Parallel.For(startPos, Input.Length, i =>
		{
			if (lz && i >= startPos + lzPos && Result[i][0].Lower + Result[i][0].Length == Result[i][0].Base)
				Result[i] = new(Result[i]) { [0] = lzInterval };
			else
				Result[i] = new(Result[i]) { [0] = i >= startPos + lzPos ? frequencyIntervals[indexCodes[i - startPos]] : new(frequencyIntervals[indexCodes[i - startPos]]) { Base = arithmeticMap[^1] } };
			Status[TN]++;
		});
		indexCodes.Dispose();
	}

	private static NList<uint> GetArithmeticMap(NList<int> frequency)
	{
		uint a = 0;
		return frequency.ToNList(x => a += (uint)x);
	}
}

using LZEntry = System.Collections.Generic.KeyValuePair<uint, (uint dist, uint length, uint spiralLength)>;
using LZStats = (Corlib.NStar.NList<uint> starts, Corlib.NStar.NList<uint> dists, Corlib.NStar.NList<uint> lengths, Corlib.NStar.NList<uint> spiralLengths);

namespace AresGlobalMethods;

/// <summary>
/// Класс, выполняющий сжатие одной из огромного множества вариаций алгоритма Лемпеля-Зива, основанных на оригинальном LZ77
/// (подробнее об этом см. <a href="https://ru.wikipedia.org/wiki/LZ77">здесь</a>).<br/>
/// Ближайшая к нашей более-менее широко известная вариация - <a href="https://ru.wikipedia.org/wiki/LZSS">LZSS</a>, поэтому
/// у нас есть два вида элементов - "непосредственные элементы", не только не сжатые, но и раздутые из-за индикатора, что это
/// именно непосредственный элемент, и "блоки Лемпеля-Зива" - ссылки на предыдущее вхождение повторяющегося фрагмента, состоящие
/// из его длины и расстояния до него и предваренные индикатором, отличным от индикатора непосредственного элемента.
/// Но наша версия Лемпеля-Зива арифметическая (см.
/// <a href="https://github.com/Etyuhibosecyu/AresTools">здесь</a>, ниже списка файлов), поэтому интервал для непосредственных
/// элементов длиннее, чем для блоков Лемпеля-Зива (так как очевидно, что в среднем файле они встречаются чаще), и у каждого
/// конкретного непосредственного элемента "нижняя граница" равна значению этого элемента до сжатия, в равной степени как и
/// "длина", а вот к "основанию" прибавляется специальное число, называемое "буфером", и этот "буферный" интервал
/// (см. <see cref="GetBufferInterval"/>) как раз и является индикатором блока Лемпеля-Зива.<br/>
/// Использование: <tt>new LempelZiv(input, result, tn[, cout]).Encode(out var lzData);</tt>.<br/>
/// </summary>
/// <param name="Input">Входной поток для сжатия.</param>
/// <param name="Result">В<u>ы</u>ходной поток для сжатых данных.</param>
/// <param name="TN">Номер потока (см. <see cref="Threads"/>).</param>
/// <param name="Cout">Нефункциональная переменная, предполагается, что когда-то будет использоваться в Ares I.</param>
/// <remarks>
/// Как привести входной поток к виду, приемлемому для этого класса, см. в проекте AresFLib в файле RootMethodsF.cs
/// в методах PreEncode() и LZEncode().
/// </remarks>
public record class LempelZiv(NList<ShortIntervalList> Input, NList<ShortIntervalList> Result, int TN, bool Cout = false)
{
	private static readonly int threadsCount = Environment.ProcessorCount;
	private readonly int huffmanIndex = Input[0].IndexOf(HuffmanApplied);
	private readonly bool huffman = Input[0].IndexOf(HuffmanApplied) != -1;
	private readonly bool pixels = Input[0].Length >= 1 && Input[0][0] == PixelsApplied;
	private readonly bool lw = Input[0].Length >= 2 && Input[0][1] == LWApplied;
	private readonly bool spaces = Input[0].Length >= 2 && Input[0][1] == SpacesApplied;
	private LZData lzData;

	/// <summary>Основной метод класса. Инструкция по применению - см. в описании класса.</summary>
	public NList<ShortIntervalList> Encode(out LZData lzData)
	{
		lzData = new();
		var lzStart = 3 + (huffman ? (int)Input[0][huffmanIndex + 1].Base : 0) + (Input[0].Length >= 1 && Input[0][0] == LengthsApplied ? (int)Input[0][1].Base : pixels ? 2 : 0);
		if (Input.Length <= lzStart)
			return LempelZivDummy(Input);
		if (CreateVar(Input[0].IndexOf(LempelZivApplied), out var lzIndex) != -1 && !(huffmanIndex != -1 && lzIndex == huffmanIndex + 1) && !(CreateVar(Input[0].IndexOf(BWTApplied), out var bwtIndex) != -1 && lzIndex == bwtIndex + 1))
			return Input;
		var result = huffman || Input[0].Length >= 1 && Input[0][0] == WordsApplied || pixels ? EncodeInts(lzStart) : EncodeBytes(lzStart);
		lzData = this.lzData;
		return result;
	}

	private NList<ShortIntervalList> EncodeInts(int lzStart)
	{
		var complexCodes = huffman && lw ? Input.GetRange(lzStart - 2).RepresentIntoNumbers((x, y) => RedStarLinq.Equals(x, y), x => x.Length switch
		{
			0 => 1234567890,
			1 => x[0].GetHashCode(),
			2 => x[0].GetHashCode() << 7 ^ x[1].GetHashCode(),
			_ => (x[0].GetHashCode() << 7 ^ x[1].GetHashCode()) << 7 ^ x[^1].GetHashCode(),
		}).ToNList(x => ((uint)x, 0u)) : Input.GetRange(lzStart - 2).ToNList((x, index) => (pixels && !(huffman || Cout) ? x[0].Lower << 24 | x[1].Lower << 16 | x[2].Lower << 8 | x[3].Lower : x[0].Lower << 9 ^ x[0].Base, spaces ? x[^1].Lower : !pixels ? 0 : x.Length >= (huffman || Cout ? 4 : 7) ? (x[^3].Lower & ValuesInByte >> 1) + (ValuesInByte >> 1) << 9 | x[^2].Lower << 8 | x[^1].Lower : x.Length >= (huffman || Cout ? 2 : 5) ? x[^1].Lower : 0));
		var (preIndexCodes, secondaryCodes) = complexCodes.NBreak();
		var secondaryCodesActive = secondaryCodes.Any(x => x != 0);
		Current[TN] = 0;
		CurrentMaximum[TN] = ProgressBarStep * 2;
		var combined = preIndexCodes.NPairs((x, y) => (ulong)x << 32 | y);
		var indexCodesList = combined.PGroup(TN, new EComparer<ulong>((x, y) => x == y, x => unchecked((17 * 23 + (int)(x >> 32)) * 23 + (int)x))).FilterInPlace(x => x.Group.Length >= 2);
		combined.Dispose();
		var indexCodes = indexCodesList.Sort(x => x.Key).PToArray(col => col.Group.Sort());
		indexCodesList.Dispose();
		var startKGlobal = 2;
		var repeatsInfo = RedStarLinq.FillArray(threadsCount, _ => new Dictionary<uint, (uint dist, uint length, uint spiralLength)>(65));
		uint useSpiralLengths = 0;
		var maxLevel = Max(BitsCount(LZDictionarySize) / 2 - 5, 0);
		Status[TN] = 0;
		StatusMaximum[TN] = indexCodes.Sum(x => x.Length) * (maxLevel + 1);
		Current[TN] += ProgressBarStep;
		var lockObj = RedStarLinq.FillArray(threadsCount, _ => new object());
		Parallel.ForEach(indexCodes, x => FindMatchesRecursive(x, 0));
		complexCodes.Dispose();
		preIndexCodes.Dispose();
		secondaryCodes.Dispose();
		var repeatsInfoSum = repeatsInfo.ConvertAndJoin(x => x).ToNList();
		return WriteLZ(Input, lzStart, repeatsInfoSum, useSpiralLengths);
		void FindMatchesRecursive(NList<uint> ic, int level)
		{
			if (level < maxLevel)
			{
				var nextCodes = ic.GetSlice(..(ic[^1] == preIndexCodes.Length - level - startKGlobal ? ^1 : ^0)).GroupIndexes(iIC => preIndexCodes[(int)iIC + level + startKGlobal]).FilterInPlace(x => x.Length >= 2).Convert(x => x.ToNList(index => ic[index]).Sort());
				nextCodes.ForEach(x => FindMatchesRecursive(x, level + 1));
				FindMatches2(ic, level + startKGlobal);
			}
			else if (ic.Length > 1)
				FindMatches(ic, level + startKGlobal);
		}
		void FindMatches(NList<uint> ic, int startK)
		{
			var nextTarget = 0;
			for (var i = 1; i < ic.Length; i++, Status[TN]++)
			{
				var iIC = (int)ic[i];
				if (iIC < nextTarget)
					continue;
				var found = ic.BinarySearch(0, i, (uint)Max(iIC - LZDictionarySize, 0));
				var matches = ic.GetSlice((found >= 0 ? found : ~found)..(ic[i - 1] == ic[i] - 1 ? i - 1 : i)).Filter(jIC => iIC - jIC >= 2 && (!secondaryCodesActive || secondaryCodes.Compare(iIC, secondaryCodes, (int)jIC, startK) == startK)).ToNList();
				var length = 0;
				for (var j = matches.Length - 1; j >= 0; j--)
				{
					var jIC = (int)matches[j];
					if (iIC - jIC < 2)
						continue;
					var k = complexCodes.Compare(iIC + startK, complexCodes, jIC + startK) + startK;
					if (k - 2 <= length)
						continue;
					length = k - 2;
					var product = 1d;
					if (Input.GetSlice(iIC + lzStart - 2, k).All(x => (product *= x.Product(y => (double)y.Base / y.Length)) < 21d * LZDictionarySize * k))
						continue;
					var sl = (ushort)Clamp(length / (iIC - jIC) - 1, 0, ushort.MaxValue);
					UpdateRepeatsInfo(repeatsInfo, lockObj, new((uint)(iIC + lzStart - 2), ((uint)Max(iIC - jIC - length - 2, 0), (uint)Min(length, iIC - jIC - 2), sl)));
					if (sl > 0)
					{
						nextTarget = jIC + k;
						useSpiralLengths = 1;
					}
				}
			}
		}
		void FindMatches2(NList<uint> ic, int k)
		{
			for (var i = 1; i < ic.Length; i++, Status[TN]++)
			{
				var iIC = (int)ic[i];
				var jIC = (int)ic[i - 1];
				if (!(iIC - jIC >= 2 && iIC - jIC < LZDictionarySize && (!secondaryCodesActive || secondaryCodes.Compare(iIC, secondaryCodes, jIC, k) == k)))
					continue;
				var product = 1d;
				if (Input.GetSlice(iIC + lzStart - 2, k).All(x => (product *= x.Product(y => (double)y.Base / y.Length)) < 21d * LZDictionarySize * k))
					continue;
				var sl = (ushort)Clamp(k / (iIC - jIC) - 1, 0, ushort.MaxValue);
				UpdateRepeatsInfo(repeatsInfo, lockObj, new((uint)(iIC + lzStart - 2), ((uint)Max(iIC - jIC - k, 0), (uint)Min(k - 2, iIC - jIC - 2), sl)));
				if (sl > 0)
					useSpiralLengths = 1;
			}
		}
	}

	private NList<ShortIntervalList> EncodeBytes(int lzStart)
	{
		var preIndexCodes = Input.GetRange(lzStart - 2).Convert(x => (byte)x[0].Lower);
		if (preIndexCodes.Length < 5)
			return LempelZivDummy(Input);
		Current[TN] = 0;
		CurrentMaximum[TN] = ProgressBarStep * 3;
		var combined = preIndexCodes.NCombine(preIndexCodes.GetRange(1), preIndexCodes.GetRange(2), (x, y, z) => ((uint)x << 8 | y) << 8 | z);
		var indexCodesList = combined.PGroup(TN).FilterInPlace(x => x.Group.Length >= 2);
		combined.Dispose();
		var indexCodes = indexCodesList.NSort(x => x.Key).PToArray(col => col.Group.Sort());
		indexCodesList.Dispose();
		var startKGlobal = 3;
		var repeatsInfo = RedStarLinq.FillArray(threadsCount, _ => new Dictionary<uint, (uint dist, uint length, uint spiralLength)>());
		uint useSpiralLengths = 0;
		var maxLevel = Max(BitsCount(LZDictionarySize) / 2 - 5, 0);
		Status[TN] = 0;
		StatusMaximum[TN] = indexCodes.Sum(x => x.Length) * (maxLevel + 1);
		Current[TN] += ProgressBarStep;
		var lockObj = RedStarLinq.FillArray(threadsCount, _ => new object());
		Parallel.ForEach(indexCodes, x => FindMatchesRecursive(x, 0));
		preIndexCodes.Dispose();
		var repeatsInfoSum = repeatsInfo.ConvertAndJoin(x => x).ToNList();
		return WriteLZ(Input, lzStart, repeatsInfoSum, useSpiralLengths);
		void FindMatchesRecursive(NList<uint> ic, int level)
		{
			if (level < maxLevel)
			{
				var endIndex = ic[^1] == preIndexCodes.Length - level - startKGlobal ? ^1 : ^0;
				var nextCodes = ic.GetSlice(..endIndex).Group(iIC => preIndexCodes[(int)iIC + level + startKGlobal]).FilterInPlace(x => x.Length >= 2).Convert(x => x.ToNList());
				nextCodes.ForEach(x => FindMatchesRecursive(x, level + 1));
				FindMatches2(ic, level + startKGlobal);
			}
			else if (ic.Length > 1)
				FindMatches(ic, level + startKGlobal);
		}
		void FindMatches(NList<uint> ic, int startK)
		{
			var nextTarget = 0;
			for (var i = 1; i < ic.Length; i++, Status[TN]++)
			{
				var iIC = (int)ic[i];
				if (iIC < nextTarget)
					continue;
				var found = ic.BinarySearch(0, i, (uint)Max(iIC - LZDictionarySize, 0));
				var matches = ic.GetSlice((found >= 0 ? found : ~found)..(ic[i - 1] == ic[i] - 1 ? i - 1 : i));
				var length = 0;
				for (var j = matches.Length - 1; j >= 0; j--)
				{
					var jIC = (int)matches[j];
					if (iIC - jIC < 2)
						continue;
					var k = preIndexCodes.Compare(iIC + startK, preIndexCodes, jIC + startK) + startK;
					if (k - 2 <= length)
						continue;
					length = k - 2;
					var product = 1d;
					if (Input.GetSlice(iIC + lzStart - 2, k).All(x => (product *= x.Product(y => (double)y.Base / y.Length)) < 21d * LZDictionarySize * k))
						continue;
					var sl = (ushort)Clamp(length / (iIC - jIC) - 1, 0, ushort.MaxValue);
					UpdateRepeatsInfo(repeatsInfo, lockObj, new((uint)(iIC + lzStart - 2), ((uint)Max(iIC - jIC - length - 2, 0), (uint)Min(length, iIC - jIC - 2), sl)));
					if (sl > 0)
					{
						nextTarget = jIC + k;
						useSpiralLengths = 1;
					}
				}
			}
		}
		void FindMatches2(NList<uint> ic, int k)
		{
			for (var i = 1; i < ic.Length; i++, Status[TN]++)
			{
				var iIC = (int)ic[i];
				var jIC = (int)ic[i - 1];
				if (iIC - jIC < 2 || iIC - jIC >= LZDictionarySize)
					continue;
				var product = 1d;
				if (Input.GetSlice(iIC + lzStart - 2, k).All(x => (product *= x.Product(y => (double)y.Base / y.Length)) < 21d * LZDictionarySize * k))
					continue;
				var sl = (ushort)Clamp(k / (iIC - jIC) - 1, 0, ushort.MaxValue);
				UpdateRepeatsInfo(repeatsInfo, lockObj, new((uint)(iIC + lzStart - 2), ((uint)Max(iIC - jIC - k, 0), (uint)Min(k - 2, iIC - jIC - 2), sl)));
				if (sl > 0)
					useSpiralLengths = 1;
			}
		}
	}

	/// <summary>
	/// Обновляет информацию о повторах во входном потоке (повторы представлены в виде словаря определенного формата,
	/// подробнее об этих повторах см. <a href="https://ru.wikipedia.org/wiki/LZ77">здесь</a>).
	/// Этод метод является потокобезопасным и использует блокировки.
	/// </summary>
	/// <param name="repeatsInfo">Информация о повторах в виде специального словаря.</param>
	/// <param name="lockObj">Массив объектов, обеспечивающих блокировки и потокобезопасность
	/// (фактически используется лишь один из них, в зависимости от ключа (индекса первого элемента в совпадении)).</param>
	/// <param name="entry">Добавляемое (возможно, поверх старого) совпадение.</param>
	public static void UpdateRepeatsInfo(Dictionary<uint, (uint dist, uint length, uint spiralLength)>[] repeatsInfo, object[] lockObj, LZEntry entry)
	{
		var (key, (dist, length, sl)) = entry;
		if (repeatsInfo[key % threadsCount].TryGetValue(key, out var value))
		{
			if ((length + 2) * (sl + 1) > (value.length + 2) * (value.spiralLength + 1))
				lock (lockObj[key % threadsCount])
					repeatsInfo[key % threadsCount][key] = (dist, length, sl);
			return;
		}
		lock (lockObj[key % threadsCount])
		{
			if (repeatsInfo[key % threadsCount].ContainsKey(key))
				return;
			repeatsInfo[key % threadsCount].TryAdd(key, (dist, length, sl));
		}
	}

	private NList<ShortIntervalList> WriteLZ(NList<ShortIntervalList> input, int lzStart, NList<LZEntry> repeatsInfo, uint useSpiralLengths)
	{
		if (repeatsInfo.Length == 0)
			return LempelZivDummy(input);
		LZRequisites(input.Length, 1, repeatsInfo, useSpiralLengths, out var boundIndex, out var repeatsInfoList, out var stats, out var lzData);
		Result.Replace(input);
		Result[0] = new(Result[0]);
		Status[TN] = 0;
		StatusMaximum[TN] = stats.starts.Length;
		Current[TN] += ProgressBarStep;
		BitList elementsReplaced = new(Result.Length, false);
		WriteLZMatches(input, lzStart, (stats.starts[..boundIndex], stats.dists, stats.lengths, stats.spiralLengths), lzData, elementsReplaced);
		var sortedRepeatsInfo2 = repeatsInfoList.PConvert(l => l.PNBreak(x => x.Key, x => (x.Value.dist, x.Value.length, x.Value.spiralLength)));
		repeatsInfoList.ForEach(x => x.Dispose());
		repeatsInfoList.Dispose();
		var brokenRepeatsInfo = sortedRepeatsInfo2.PConvert(l => (l.Item1, l.Item2.PNBreak()));
		sortedRepeatsInfo2.ForEach(x => x.Item2.Dispose());
		sortedRepeatsInfo2.Dispose();
		void ProcessBrokenRepeats((NList<uint>, (NList<uint>, NList<uint>, NList<uint>)) x) => WriteLZMatches(input, lzStart, (x.Item1, x.Item2.Item1, x.Item2.Item2, x.Item2.Item3), lzData, elementsReplaced, false);
		if (pixels || Cout)
			brokenRepeatsInfo.ForEach(ProcessBrokenRepeats);
		else
			Parallel.ForEach(brokenRepeatsInfo, ProcessBrokenRepeats);
		Result.FilterInPlace((x, index) => index == 0 || !elementsReplaced[index]);
		elementsReplaced.Dispose();
		Result[0].Add(LempelZivApplied);
		NList<Interval> c = [new Interval(lzData.Dist.R, 3)];
		c.WriteNumber(lzData.Dist.Max);
		if (lzData.Dist.R != 0)
			c.Add(new(lzData.Dist.Threshold, lzData.Dist.Max + 1));
		c.Add(new Interval(lzData.Length.R, 3));
		c.WriteNumber(lzData.Length.Max, 16);
		if (lzData.Length.R != 0)
			c.Add(new(lzData.Length.Threshold, lzData.Length.Max + 1));
		if (lzData.Dist.Max == 0 && lzData.Length.Max == 0)
			c.Add(new(1, 2));
		c.Add(new Interval(useSpiralLengths, 2));
		if (useSpiralLengths == 1)
		{
			c.Add(new Interval(lzData.SpiralLength.R, 3));
			c.WriteNumber(lzData.SpiralLength.Max, 16);
			if (lzData.SpiralLength.R != 0)
				c.Add(new(lzData.SpiralLength.Threshold, lzData.SpiralLength.Max + 1));
		}
#if DEBUG
		var input2 = input.Skip(lzStart - 2);
		var decoded = new LempelZivDec(Result.GetRange(lzStart - 2), true, lzData, TN).Decode();
		for (var i = 0; i < input2.Length && i < decoded.Length; i++)
			for (var j = 0; j < input2[i].Length && j < decoded[i].Length; j++)
			{
				var x = input2[i][j];
				var y = decoded[i][j];
				if (!(x.Equals(y) || GetBaseWithBuffer(x.Base, spaces || pixels) == y.Base && x.Lower == y.Lower && x.Length == y.Length))
					throw new DecoderFallbackException();
			}
		if (input2.Length != decoded.Length)
			throw new DecoderFallbackException();
#endif
		if (c.Length > 8)
			Result[0].Add(LempelZivSubdivided);
		Result.Insert(1 + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : pixels ? 2 : 0), c.SplitIntoEqual(8).Convert(x => new ShortIntervalList(x)));
		return Result;
	}

	/// <summary>
	/// Генерирует основные переменные, используемые во второй части Лемпеля-Зива.
	/// </summary>
	/// <param name="inputLength">Количество элементов во входном потоке.</param>
	/// <param name="multiplier">Множитель на пороговую длину совпадения (сначала обрабатываются более длинные).</param>
	/// <param name="repeatsInfo">Словарь повторов (см. <see cref="UpdateRepeatsInfo"/>).</param>
	/// <param name="useSpiralLengths">Присутствуют ли в вЫходном потоке спиральные длины
	/// (см. <a href="https://github.com/Etyuhibosecyu/AresTools">здесь</a>, ниже списка файлов).</param>
	/// <param name="boundIndex">Индекс первого совпадения короче пороговой длины.</param>
	/// <param name="repeatsInfoParts">Части словаря повторов с короткими совпадениями.</param>
	/// <param name="stats">Статистика повторов: начала, расстояния, длины и спиральные длины.</param>
	/// <param name="lzData">См. <see cref="LZData"/>.</param>
	public static void LZRequisites(int inputLength, int multiplier, NList<LZEntry> repeatsInfo, uint useSpiralLengths, out int boundIndex, out List<NList<LZEntry>> repeatsInfoParts, out LZStats stats, out LZData lzData)
	{
		var sortedRepeatsInfo = repeatsInfo.Sort(x => x.Key).Sort(x => 4294967295 - GetMatchLength(x));
		boundIndex = sortedRepeatsInfo.FindIndex(x => GetMatchLength(x) < multiplier * 10);
		if (boundIndex == -1)
			boundIndex = sortedRepeatsInfo.Length;
		var processorBlockLength = GetArrayLength(inputLength, Environment.ProcessorCount);
		repeatsInfoParts = RedStarLinq.PFill(Environment.ProcessorCount, index => new NList<LZEntry>());
		var repeatsInfoEnd = sortedRepeatsInfo[boundIndex..];
		for (var i = 0; i < repeatsInfoEnd.Length; i++)
		{
			var x = repeatsInfoEnd[i];
			var index = (int)x.Key / processorBlockLength;
			if ((x.Key + GetMatchLength(x) - 1) / processorBlockLength == index)
				repeatsInfoParts[index].Add(x);
		}
		var brokenRepeatsInfo = sortedRepeatsInfo.PNBreak(x => x.Key, x => x.Value);
		sortedRepeatsInfo.Dispose();
		var (starts, (dists, lengths, spiralLengths)) = (brokenRepeatsInfo.Item1, brokenRepeatsInfo.Item2.PNBreak());
		brokenRepeatsInfo.Item2.Dispose();
		var sumsAndMaximums = new[] { dists, lengths, spiralLengths }.PNConvert(l => (l.Sum(), l.Max())).Wrap(x => (x[0], x[1], x[2]));
		var ((distsSum, maxDist), (lengthsSum, maxLength), (spiralLengthsSum, maxSpiralLength)) = sumsAndMaximums;
		var mediumDist = (int)Max(distsSum / dists.Length, 1);
		var (rDist, thresholdDist) = GetRAndThreshold(dists, mediumDist);
		var mediumLength = (int)Max(lengthsSum / lengths.Length, 1);
		var (rLength, thresholdLength) = GetRAndThreshold(lengths, mediumLength);
		var mediumSpiralLength = (int)Max(spiralLengthsSum / spiralLengths.Length, 1);
		var (rSpiralLength, thresholdSpiralLength) = GetRAndThreshold(spiralLengths, mediumSpiralLength);
		stats = (starts, dists, lengths, spiralLengths);
		lzData = new(new(rDist, maxDist, thresholdDist), new(rLength, maxLength, thresholdLength), useSpiralLengths, new(rSpiralLength, maxSpiralLength, thresholdSpiralLength));
	}

	private static (uint, uint) GetRAndThreshold(NList<uint> list, int medium)
	{
		var upperCount = list.Count(x => x >= medium);
		if (upperCount <= list.Length / 3)
			return (1, list.Filter(x => x < medium).Max());
		else if (upperCount > list.Length * 2 / 3)
			return (2, list.Filter(x => x >= medium).Min());
		else
			return (0, 0);
	}

	private void WriteLZMatches(NList<ShortIntervalList> input, int lzStart, LZStats stats, LZData lzData, BitList elementsReplaced, bool changeBase = true)
	{
		double statesNumLog1, statesNumLog2;
		for (var i = 0; i < stats.starts.Length; i++, Status[TN]++)
		{
			uint iDist = stats.dists[i], iLength = stats.lengths[i], iSpiralLength = stats.spiralLengths[i];
			var localMaxLength = (iLength + 2) * (iSpiralLength + 1) - 2;
			var iStart = (int)stats.starts[i];
			var oldBase = input[iStart][0].Base;
			var newBase = GetBaseWithBuffer(oldBase, spaces || pixels);
			statesNumLog1 = 0;
			if (CreateVar(elementsReplaced.IndexOf(true, iStart, Min((int)localMaxLength + 3, elementsReplaced.Length - iStart)), out var replacedIndex) != -1)
			{
				if (iSpiralLength != 0 || replacedIndex < iStart + 3)
					continue;
				iDist += (uint)(iStart + 3 + iLength - replacedIndex);
				localMaxLength = iLength = (uint)(replacedIndex - iStart - 3);
				if (iDist > lzData.Dist.Max) continue;
			}
			for (var k = iStart; k <= iStart + localMaxLength + 1; k++)
				statesNumLog1 += input[k].Sum(x => Log(x.Base) - Log(x.Length));
			statesNumLog2 = Log(newBase) - Log(newBase - oldBase);
			statesNumLog2 += StatesNumLogSum(iDist, lzData.Dist, lzData.UseSpiralLengths);
			statesNumLog2 += StatesNumLogSum(iLength, lzData.Length);
			if (lzData.UseSpiralLengths == 1 && iLength < localMaxLength)
				statesNumLog2 += StatesNumLogSum(iSpiralLength, lzData.SpiralLength);
			if (statesNumLog1 <= statesNumLog2)
				continue;
			ShortIntervalList b = [new(oldBase, newBase - oldBase, newBase)];
			WriteLZValue(b, iLength, lzData.Length);
			var maxDist2 = Min(lzData.Dist.Max, (uint)(iStart - iLength - lzStart));
			if (lzData.UseSpiralLengths == 0)
				WriteLZValue(b, iDist, new(lzData.Dist.R, maxDist2, lzData.Dist.Threshold));
			else if (iLength >= localMaxLength)
				WriteLZValue(b, iDist, new(lzData.Dist.R, maxDist2, lzData.Dist.Threshold), 1);
			else
			{
				if (lzData.Dist.R == 0 || maxDist2 < lzData.Dist.Threshold)
					b.Add(new(maxDist2 + 1, maxDist2 + 2));
				else if (lzData.Dist.R == 1)
				{
					b.Add(new(lzData.Dist.Threshold + 1, lzData.Dist.Threshold + 2));
					b.Add(new(maxDist2 - lzData.Dist.Threshold, maxDist2 - lzData.Dist.Threshold + 1));
				}
				else
				{
					b.Add(new(maxDist2 - lzData.Dist.Threshold + 1, maxDist2 - lzData.Dist.Threshold + 2));
					b.Add(new(lzData.Dist.Threshold, lzData.Dist.Threshold + 1));
				}
			}
			if (lzData.UseSpiralLengths == 1 && iLength < localMaxLength)
				WriteLZValue(b, iSpiralLength, lzData.SpiralLength);
			Result[iStart] = new(b);
			elementsReplaced.SetAll(true, iStart + 1, (int)localMaxLength + 1);
		}
		stats.starts.Dispose();
		stats.dists.Dispose();
		stats.lengths.Dispose();
		stats.spiralLengths.Dispose();
		if (!changeBase)
			return;
		Parallel.For(lzStart, Result.Length, i =>
		{
			if (!elementsReplaced[i] && (i == Result.Length - 1 || !elementsReplaced[i + 1]))
			{
				var first = Result[i][0];
				var newBase = GetBaseWithBuffer(first.Base, spaces || pixels);
				Result[i] = !pixels && newBase == 269 ? ByteIntervals2[(int)first.Lower] : new(Result[i]) { [0] = new(first.Lower, first.Length, newBase) };
			}
		});
	}

	/// <summary>
	/// Возвращает логарифм числа "состояний" блока Лемпеля-Зива (условная величина,
	/// позволяющая оценить, выгодно ли заменять повторяющиеся элементы таким блоком).
	/// </summary>
	/// <param name="value">Расстояние, длина или спиральная длина.</param>
	/// <param name="data">См. <see cref="MethodDataUnit"/>.</param>
	/// <param name="extraShift">1, если используются спиральные длины, иначе 0.</param>
	public static double StatesNumLogSum(uint value, MethodDataUnit data, uint extraShift = 0)
	{
		double sum = 0;
		if (data.R == 0)
			sum += Log(data.Max + 1 + extraShift);
		else if (data.R == 1)
		{
			sum += Log(data.Threshold + 2);
			if (value > data.Threshold)
				sum += Log(data.Max - data.Threshold + 1 + extraShift);
		}
		else
		{
			sum += Log(data.Max - data.Threshold + 2);
			if (value < data.Threshold)
				sum += Log(data.Threshold + extraShift);
		}
		return sum;
	}

	/// <summary>
	/// Записывает значение расстояния, длины или спиральной длины в блок Лемпеля-Зива.
	/// </summary>
	/// <param name="list">Блок Лемпеля-Зива, в который происходит запись.</param>
	/// <param name="value">Расстояние, длина или спиральная длина.</param>
	/// <param name="data">См. <see cref="MethodDataUnit"/>.</param>
	/// <param name="extraShift">1, если используются спиральные длины, иначе 0.</param>
	public static void WriteLZValue(ShortIntervalList list, uint value, MethodDataUnit data, uint extraShift = 0)
	{
		if (data.R == 0 || data.Max < data.Threshold)
			list.Add(new(value, data.Max + 1 + extraShift));
		else if (data.R == 1)
		{
			if (value <= data.Threshold)
				list.Add(new(value, data.Threshold + 2));
			else
			{
				list.Add(new(data.Threshold + 1, data.Threshold + 2));
				list.Add(new(value - data.Threshold - 1, data.Max - data.Threshold + extraShift));
			}
		}
		else
		{
			if (value >= data.Threshold)
				list.Add(new(value - data.Threshold, data.Max - data.Threshold + 2));
			else
			{
				list.Add(new(data.Max - data.Threshold + 1, data.Max - data.Threshold + 2));
				list.Add(new(value, data.Threshold + extraShift));
			}
		}
	}

	private static uint GetMatchLength(LZEntry x) => (x.Value.length + 2) * (x.Value.spiralLength + 1);

	private NList<ShortIntervalList> LempelZivDummy(NList<ShortIntervalList> input)
	{
		Result.Replace(input);
		Result[0] = new(Result[0]) { LempelZivDummyApplied };
		NList<Interval> list = [new(0, 3)];
		list.WriteNumber(0);
		list.Add(new(0, 3));
		list.WriteNumber(0, 16);
		list.Add(new(0, 2));
		Result.Insert(1 + (input[0].Length >= 1 && input[0][0] == LengthsApplied ? (int)input[0][1].Base : 0), new ShortIntervalList(list));
		return Result;
	}
}

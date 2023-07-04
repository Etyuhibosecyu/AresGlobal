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

namespace UnsafeFunctions;

public static unsafe class Global
{
	public enum UsedMethods
	{
		None = 0,
		CS1 = 1,
		LZ1 = 1 << 1,
		HF1 = 1 << 2,
		//Dev1 = 1 << 3,
		PSLZ1 = 1 << 4,
		CS2 = 1 << 5,
		//Dev2 = 1 << 6,
		LZ2 = 1 << 7,
		SHET2 = 1 << 8,
		CS3 = 1 << 9,
		//Dev3 = 1 << 10,
		//Dev3_2 = 1 << 11,
		CS4 = 1 << 12,
		//Dev4 = 1 << 13,
		SHET4 = 1 << 14,
		CS5 = 1 << 15,
		SHET5 = 1 << 16,
		CS6 = 1 << 17,
		CS7 = 1 << 18,
		SHET7 = 1 << 19,
		CS8 = 1 << 20,
		AHF = 1 << 21,
	}
	public const byte ProgramVersion = 1;
	public const int MillisecondsPerSecond = 1000;
	public const int ProgressBarStep = 10;
	public const int BitsPerByte = 8;
	public const int ValuesInByte = 1 << BitsPerByte;
	public const int ValuesIn2Bytes = ValuesInByte * ValuesInByte;
	public const int ValuesIn3Bytes = ValuesIn2Bytes * ValuesInByte;
	public const int FragmentLength = 8000000;
	public const int BWTBlockSize = 50000;
	public const uint HuffmanApplied = 10;
	public const uint LempelZivApplied = 11;
	public const uint PPMWApplied = 12;
	public const uint SpacesApplied = 13;
	public const uint WordsApplied = 14;
	public const uint LengthsApplied = 15;
	public const uint BWTApplied = 16;
	public const uint LempelZivSubdivided = 20;
	public const uint LempelZivDummyApplied = 21;
	public const int ProgressBarHGroups = 3, ProgressBarVGroups = 3, ProgressBarGroups = ProgressBarHGroups * ProgressBarVGroups;
	public static Thread[] Threads { get; set; } = new Thread[ProgressBarGroups];
	public static int Supertotal { get; set; }
	public static int SupertotalMaximum { get; set; }
	public static int Total { get; set; }
	public static int TotalMaximum { get; set; }
	public static int[] Subtotal { get; set; } = new int[ProgressBarGroups];
	public static int[] SubtotalMaximum { get; set; } = new int[ProgressBarGroups];
	public static int[] Current { get; set; } = new int[ProgressBarGroups];
	public static int[] CurrentMaximum { get; set; } = new int[ProgressBarGroups];
	public static int[] Status { get; set; } = new int[ProgressBarGroups];
	public static int[] StatusMaximum { get; set; } = new int[ProgressBarGroups];
	public static UsedMethods PresentMethods { get; set; } = UsedMethods.CS1 | UsedMethods.HF1 | UsedMethods.LZ1 | UsedMethods.CS2 | UsedMethods.LZ2;
	public static byte BWTThreads { get; set; }
	public static string[][] SHETEndinds { get; } = new[] { new[] { "а", "я", "ы", "и", "е", "у", "ю", "ой", "ей", "ам", "ям", "ами", "ями", "ах", "ях", "о", "ь", "ом", "ем", "ём", "ов", "ью", "ени", "енем", "ен", "ён", "ян", "енам", "енами", "енах" }, new[] { "ый", "ий", "ого", "его", "ому", "ему", "ым", "им", "ая", "яя", "ую", "юю", "ою", "ею", "ое", "ее", "ые", "ие", "ых", "их", "ной", "ный", "ний", "ного", "него", "ному", "нему", "ным", "ним", "ном", "нем", "ная", "няя", "ней", "ную", "нюю", "ною", "нею", "ное", "нее", "ные", "ние", "ных", "них", "он", "на", "ня", "но", "нё", "ны", "ни" }, new[] { "ть", "ать", "еть", "ить", "оть", "уть", "ыть", "ять", "овать", "евать", "л", "ал", "ел", "ил", "ол", "ул", "ыл", "ял", "овал", "евал", "ла", "ала", "ела", "ила", "ола", "ула", "ыла", "яла", "овала", "евала", "ло", "ало", "ело", "ило", "оло", "уло", "ыло", "яло", "овало", "евало", "ли", "али", "ели", "или", "оли", "ули", "ыли", "яли", "овали", "евали", "ишь", "ит", "ите", "ешь", "ёшь", "ет", "ёт", "ете", "ёте", "аешь", "аёшь", "ает", "аёт", "аем", "аём", "аете", "аёте", "еешь", "еет", "еем", "еете", "иешь", "иёшь", "иет", "иёт", "ием", "иём", "иете", "иёте", "оешь", "оет", "оем", "оете", "уешь", "уёшь", "ует", "уёт", "уем", "уём", "уете", "уёте", "ьешь", "ьёшь", "ьет", "ьёт", "ьем", "ьём", "ьете", "ьёте", "юешь", "юёшь", "юет", "юёт", "юем", "юём", "юете", "юёте", "яешь", "яет", "яем", "яете", "ут", "ют", "ают", "еют", "иют", "оют", "уют", "ьют", "юют", "яют", "ат", "ят", "ться", "аться", "еться", "иться", "оться", "уться", "ыться", "яться", "оваться", "еваться", "лся", "ался", "елся", "ился", "олся", "улся", "ылся", "ялся", "овался", "евался", "лась", "алась", "елась", "илась", "олась", "улась", "ылась", "ялась", "овалась", "евалась", "лось", "алось", "елось", "илось", "олось", "улось", "ылось", "ялось", "овалось", "евалось", "лись", "ались", "елись", "ились", "олись", "улись", "ылись", "ялись", "овались", "евались", "усь", "юсь", "ишься", "ится", "имся", "итесь", "ешься", "ёшься", "ется", "ётся", "емся", "ёмся", "етесь", "ётесь", "аешься", "аёшься", "ается", "аётся", "аемся", "аёмся", "аетесь", "аётесь", "еешься", "еется", "еемся", "еетесь", "иется", "иётся", "иемся", "иёмся", "иетесь", "иётесь", "оешься", "оется", "оемся", "оетесь", "уешься", "уёшься", "уется", "уётся", "уемся", "уёмся", "уетесь", "уётесь", "ьешься", "ьёшься", "ьется", "ьётся", "ьемся", "ьёмся", "ьетесь", "ьётесь", "юешься", "юёшься", "юется", "юётся", "юемся", "юёмся", "юетесь", "юётесь", "яешься", "яется", "яемся", "яетесь", "утся", "ются", "аются", "еются", "иются", "оются", "уются", "ьются", "юются", "яются", "атся", "ятся", "й", "ай", "ей", "ой", "уй", "ый", "яй", "йся", "айся", "ейся", "ойся", "уйся", "ыйся", "яйся", "ись", "ься", "ущий", "ющий", "ающий", "еющий", "иющий", "оющий", "ующий", "ьющий", "юющий", "яющий", "ащий", "ящий", "ущего", "ющего", "ающего", "еющего", "иющего", "оющего", "ующего", "ьющего", "юющего", "яющего", "ащего", "ящего", "ущему", "ющему", "ающему", "еющему", "иющему", "оющему", "ующему", "ьющему", "юющему", "яющему", "ащему", "ящему", "ущим", "ющим", "ающим", "еющим", "иющим", "оющим", "ующим", "ьющим", "юющим", "яющим", "ащим", "ящим", "ущем", "ющем", "ающем", "еющем", "иющем", "оющем", "ующем", "ьющем", "юющем", "яющем", "ащем", "ящем", "ущая", "ющая", "ающая", "еющая", "иющая", "оющая", "ующая", "ьющая", "юющая", "яющая", "ащая", "ящая", "ущей", "ющей", "ающей", "еющей", "иющей", "оющей", "ующей", "ьющей", "юющей", "яющей", "ащей", "ящей", "ущую", "ющую", "ающую", "еющую", "иющую", "оющую", "ующую", "ьющую", "юющую", "яющую", "ащую", "ящую", "ущее", "ющее", "ающее", "еющее", "иющее", "оющее", "ующее", "ьющее", "юющее", "яющее", "ащее", "ящее", "ущие", "ющие", "ающие", "еющие", "иющие", "оющие", "ующие", "ьющие", "юющие", "яющие", "ащие", "ящие", "ущих", "ющих", "ающих", "еющих", "иющих", "оющих", "ующих", "ьющих", "юющих", "яющих", "ащих", "ящих", "ущими", "ющими", "ающими", "еющими", "иющими", "оющими", "ующими", "ьющими", "юющими", "яющими", "ащими", "ящими", "ущийся", "ющийся", "ающийся", "еющийся", "иющийся", "оющийся", "ующийся", "ьющийся", "юющийся", "яющийся", "ащийся", "ящийся", "ущегося", "ющегося", "ающегося", "еющегося", "иющегося", "оющегося", "ующегося", "ьющегося", "юющегося", "яющегося", "ащегося", "ящегося", "ущемуся", "ющемуся", "ающемуся", "еющемуся", "иющемуся", "оющемуся", "ующемуся", "ьющемуся", "юющемуся", "яющемуся", "ащемуся", "ящемуся", "ущимся", "ющимся", "ающимся", "еющимся", "иющимся", "оющимся", "ующимся", "ьющимся", "юющимся", "яющимся", "ащимся", "ящимся", "ущемся", "ющемся", "ающемся", "еющемся", "иющемся", "оющемся", "ующемся", "ьющемся", "юющемся", "яющемся", "ащемся", "ящемся", "ущаяся", "ющаяся", "ающаяся", "еющаяся", "иющаяся", "оющаяся", "ующаяся", "ьющаяся", "юющаяся", "яющаяся", "ащаяся", "ящаяся", "ущейся", "ющейся", "ающейся", "еющейся", "иющейся", "оющейся", "ующейся", "ьющейся", "юющейся", "яющейся", "ащейся", "ящейся", "ущуюся", "ющуюся", "ающуюся", "еющуюся", "иющуюся", "оющуюся", "ующуюся", "ьющуюся", "юющуюся", "яющуюся", "ащуюся", "ящуюся", "ущееся", "ющееся", "ающееся", "еющееся", "иющееся", "оющееся", "ующееся", "ьющееся", "юющееся", "яющееся", "ащееся", "ящееся", "ущиеся", "ющиеся", "ающиеся", "еющиеся", "иющиеся", "оющиеся", "ующиеся", "ьющиеся", "юющиеся", "яющиеся", "ащиеся", "ящиеся", "ущихся", "ющихся", "ающихся", "еющихся", "иющихся", "оющихся", "ующихся", "ьющихся", "юющихся", "яющихся", "ащихся", "ящихся", "ущимися", "ющимися", "ающимися", "еющимися", "иющимися", "оющимися", "ующимися", "ьющимися", "юющимися", "яющимися", "ащимися", "ящимися", "вший", "авший", "евший", "ивший", "овший", "увший", "ывший", "явший", "овавший", "евавший", "вшего", "авшего", "евшего", "ившего", "овшего", "увшего", "ывшего", "явшего", "овавшего", "евавшего", "вшему", "авшему", "евшему", "ившему", "овшему", "увшему", "ывшему", "явшему", "овавшему", "евавшему", "вшим", "авшим", "евшим", "ившим", "овшим", "увшим", "ывшим", "явшим", "овавшим", "евавшим", "вшем", "авшем", "евшем", "ившем", "овшем", "увшем", "ывшем", "явшем", "овавшем", "евавшем", "вшая", "авшая", "евшая", "ившая", "овшая", "увшая", "ывшая", "явшая", "овавшая", "евавшая", "вшей", "авшей", "евшей", "ившей", "овшей", "увшей", "ывшей", "явшей", "овавшей", "евавшей", "вшую", "авшую", "евшую", "ившую", "овшую", "увшую", "ывшую", "явшую", "овавшую", "евавшую", "вшее", "авшее", "евшее", "ившее", "овшее", "увшее", "ывшее", "явшее", "овавшее", "евавшее", "вшие", "авшие", "евшие", "ившие", "овшие", "увшие", "ывшие", "явшие", "овавшие", "евавшие", "вших", "авших", "евших", "ивших", "овших", "увших", "ывших", "явших", "овавших", "евавших", "вшими", "авшими", "евшими", "ившими", "овшими", "увшими", "ывшими", "явшими", "овавшими", "евавшими", "вшийся", "авшийся", "евшийся", "ившийся", "овшийся", "увшийся", "ывшийся", "явшийся", "овавшийся", "евавшийся", "вшегося", "авшегося", "евшегося", "ившегося", "овшегося", "увшегося", "ывшегося", "явшегося", "овавшегося", "евавшегося", "вшемуся", "авшемуся", "евшемуся", "ившемуся", "овшемуся", "увшемуся", "ывшемуся", "явшемуся", "овавшемуся", "евавшемуся", "вшимся", "авшимся", "евшимся", "ившимся", "овшимся", "увшимся", "ывшимся", "явшимся", "овавшимся", "евавшимся", "вшемся", "авшемся", "евшемся", "ившемся", "овшемся", "увшемся", "ывшемся", "явшемся", "овавшемся", "евавшемся", "вшаяся", "авшаяся", "евшаяся", "ившаяся", "овшаяся", "увшаяся", "ывшаяся", "явшаяся", "овавшаяся", "евавшаяся", "вшейся", "авшейся", "евшейся", "ившейся", "овшейся", "увшейся", "ывшейся", "явшейся", "овавшейся", "евавшейся", "вшуюся", "авшуюся", "евшуюся", "ившуюся", "овшуюся", "увшуюся", "ывшуюся", "явшуюся", "овавшуюся", "евавшуюся", "вшееся", "авшееся", "евшееся", "ившееся", "овшееся", "увшееся", "ывшееся", "явшееся", "овавшееся", "евавшееся", "вшиеся", "авшиеся", "евшиеся", "ившиеся", "овшиеся", "увшиеся", "ывшиеся", "явшиеся", "овавшиеся", "евавшиеся", "вшихся", "авшихся", "евшихся", "ившихся", "овшихся", "увшихся", "ывшихся", "явшихся", "овавшихся", "евавшихся", "вшимися", "авшимися", "евшимися", "ившимися", "овшимися", "увшимися", "ывшимися", "явшимися", "овавшимися", "евавшимися",
			"имый", "емый", "аемый", "еемый", "оемый", "уемый", "юемый", "яемый", "омый", "имого", "емого", "аемого", "еемого", "оемого", "уемого", "юемого", "яемого", "омого", "имому", "емому", "аемому", "еемому", "оемому", "уемому", "юемому", "яемому", "омому", "имым", "емым", "аемым", "еемым", "оемым", "уемым", "юемым", "яемым", "омым", "имом", "емом", "аемом", "еемом", "оемом", "уемом", "юемом", "яемом", "омом", "имая", "емая", "аемая", "еемая", "оемая", "уемая", "юемая", "яемая", "омая", "имой", "емой", "аемой", "еемой", "оемой", "уемой", "юемой", "яемой", "омой", "имую", "емую", "аемую", "еемую", "оемую", "уемую", "юемую", "яемую", "омую", "имое", "емое", "аемое", "еемое", "оемое", "уемое", "юемое", "яемое", "омое", "имые", "емые", "аемые", "еемые", "оемые", "уемые", "юемые", "яемые", "омые", "имых", "емых", "аемых", "еемых", "оемых", "уемых", "юемых", "яемых", "омых", "имыми", "емыми", "аемыми", "еемыми", "оемыми", "уемыми", "юемыми", "яемыми", "омыми", "аный", "еный", "ёный", "яный", "ованый", "еваный", "ёваный", "анный", "енный", "ённый", "янный", "ованный", "еванный", "ёванный", "аного", "еного", "ёного", "яного", "ованого", "еваного", "ёваного", "анного", "енного", "ённого", "янного", "ованного", "еванного", "ёванного", "аному", "еному", "ёному", "яному", "ованому", "еваному", "ёваному", "анному", "енному", "ённому", "янному", "ованному", "еванному", "ёванному", "аным", "еным", "ёным", "яным", "ованым", "еваным", "ёваным", "анным", "енным", "ённым", "янным", "ованным", "еванным", "ёванным", "аном", "еном", "ёном", "яном", "ованом", "еваном", "ёваном", "анном", "енном", "ённом", "янном", "ованном", "еванном", "ёванном", "аная", "еная", "ёная", "яная", "ованая", "еваная", "ёваная", "анная", "енная", "ённая", "янная", "ованная", "еванная", "ёванная", "аной", "еной", "ёной", "яной", "ованой", "еваной", "ёваной", "анной", "енной", "ённой", "янной", "ованной", "еванной", "ёванной", "аную", "еную", "ёную", "яную", "ованую", "еваную", "ёваную", "анную", "енную", "ённую", "янную", "ованную", "еванную", "ёванную", "аное", "еное", "ёное", "яное", "ованое", "еваное", "ёваное", "анное", "енное", "ённое", "янное", "ованное", "еванное", "ёванное", "аные", "еные", "ёные", "яные", "ованые", "еваные", "ёваные", "анные", "енные", "ённые", "янные", "ованные", "еванные", "ёванные", "аных", "еных", "ёных", "яных", "ованых", "еваных", "ёваных", "анных", "енных", "ённых", "янных", "ованных", "еванных", "ёванных", "аными", "еными", "ёными", "яными", "оваными", "еваными", "ёваными", "анными", "енными", "ёнными", "янными", "ованными", "еванными", "ёванными", "тый", "атый", "етый", "итый", "отый", "утый", "ытый", "ятый", "того", "атого", "етого", "итого", "отого", "утого", "ытого", "ятого", "тому", "атому", "етому", "итому", "отому", "утому", "ытому", "ятому", "тым", "атым", "етым", "итым", "отым", "утым", "ытым", "ятым", "том", "атом", "етом", "итом", "отом", "утом", "ытом", "ятом", "тая", "атая", "етая", "итая", "отая", "утая", "ытая", "ятая", "той", "атой", "етой", "итой", "отой", "утой", "ытой", "ятой", "тую", "атую", "етую", "итую", "отую", "утую", "ытую", "ятую", "тое", "атое", "етое", "итое", "отое", "утое", "ытое", "ятое", "тые", "атые", "етые", "итые", "отые", "утые", "ытые", "ятые", "тых", "атых", "етых", "итых", "отых", "утых", "ытых", "ятых", "тыми", "атыми", "етыми", "итыми", "отыми", "утыми", "ытыми", "ятыми", "учи", "ючи", "аючи", "еючи", "иючи", "оючи", "уючи", "ьючи", "юючи", "яючи", "в", "ав", "ев", "ив", "ув", "ыв", "яв", "овав", "евав", "вши", "авши", "евши", "ивши", "овши", "увши", "ывши", "явши", "овавши", "евавши", "ши", "ась", "ясь", "учись", "ючись", "аючись", "еючись", "иючись", "оючись", "уючись", "ьючись", "юючись", "яючись", "вшись", "авшись", "евшись", "ившись", "овшись", "увшись", "ывшись", "явшись", "овавшись", "евавшись", "шись" }, new[] { "без", "безо", "близ", "в", "вблизи", "ввиду", "вглубь", "вдогон", "вдоль", "взамен", "включая", "вкруг", "вместо", "вне", "внизу", "внутри", "внутрь", "во", "вовнутрь", "возле", "вокруг", "вопреки", "впереди", "вроде", "вслед", "вследствие", "встречу", "выключая", "для", "до", "за", "заместо", "из", "изнутри", "изо", "исключая", "к", "касаемо", "касательно", "ко", "кончая", "кроме", "кругом", "меж", "между", "мимо", "на", "наверху", "навстречу", "над", "надо", "назад", "назло", "накануне", "наперекор", "наперерез", "наподобие", "напротив", "насчёт", "ниже", "о", "об", "обо", "обок", "около", "от", "относительно", "ото", "перед", "передо", "по", "поверх", "под", "под видом", "подле", "подо", "подобно", "позади", "помимо", "поперёд", "поперёк", "порядка", "посередине", "после", "посреди", "посредине", "посредством", "пред", "предо", "прежде", "при", "про", "против", "путём", "ради", "с", "сверх", "сверху", "свыше", "сзади", "сквозь", "снизу", "со", "согласно", "спустя", "среди", "средь", "сродни", "супротив", "у", "через", "черезо", "чрез" } };
	public static Dictionary<string, int> SHETDic1 { get; } = SHETEndinds.AsSpan(0, 3).JoinIntoSingle().Filter(x => x.Length > 2).Wrap(x => (x, new Chain(x.Length)).ToDictionary());
	public static int SHETThreshold1 { get; } = ValuesInByte - (SHETDic1.Length == ValuesInByte ? 0 : Max(SHETDic1.Length, 0) / ValuesInByte);
	public static Dictionary<string, int> SHETDic2 { get; } = SHETEndinds[3].Filter(x => x.Length > 2).Wrap(x => (x, new Chain(x.Length)).ToDictionary());
	public static int SHETThreshold2 { get; } = ValuesInByte - (SHETDic2.Length == ValuesInByte ? 0 : Max(SHETDic2.Length, 0) / ValuesInByte);
	public static List<ShortIntervalList> ByteIntervals { get; } = RedStarLinq.Fill(ValuesInByte, index => new ShortIntervalList() { new Interval((uint)index, ValuesInByte) });
	public static List<ShortIntervalList> ByteIntervals2 { get; } = RedStarLinq.Fill(ValuesInByte, index => new ShortIntervalList() { new Interval((uint)index, 269) });
	public static uint[] FibonacciSequence { get; } = new uint[] { 1, 2, 3, 5, 8, 13, 21, 34, 55, 89, 144, 233, 377, 610, 987, 1597, 2584, 4181, 6765, 10946, 17711, 28657, 46368, 75025, 121393, 196418, 317811, 514229, 832040, 1346269, 2178309, 3524578, 5702887, 9227465, 14930352, 24157817, 39088169, 63245986, 102334155, 165580141, 267914296, 433494437, 701408733, 1134903170, 1836311903, 2971215073 };

	public class IntListComparer : G.IComparer<List<int>>
	{
		public int Compare(List<int>? x, List<int>? y)
		{
			if (x == null && y == null)
				return 0;
			else if (x == null)
				return 1;
			else if (y == null)
				return -1;
			var n = Min(x.Length, y.Length);
			for (var i = 0; i < n; i++)
			{
				if (x[i] > y[i])
					return 1;
				else if (x[i] < y[i])
					return -1;
			}
			if (x.Length > y.Length)
				return 1;
			else if (x.Length < y.Length)
				return -1;
			return 0;
		}
	}

	public static List<(uint[] Group, TSource Key)> PGroup<TSource>(this G.IList<TSource> source, int tn, G.IEqualityComparer<TSource>? comparer = null)
	{
		var lockObj = RedStarLinq.FillArray(Environment.ProcessorCount, x => new object());
		var lockObj2 = RedStarLinq.FillArray(Environment.ProcessorCount, x => new object());
		var count = source.Count;
		var innerIndexes = (int*)Marshal.AllocHGlobal(sizeof(int) * count);
		FillMemory(innerIndexes, count, 0);
		ParallelHashSet<TSource> hs = new(comparer);
		Status[tn] = 0;
		StatusMaximum[tn] = count;
		Parallel.For(0, count, i =>
		{
			hs.TryAdd(source[i], out innerIndexes[i]);
			Status[tn]++;
		});
		var dicKeys = hs.ToArray();
		var innerCount = (int*)Marshal.AllocHGlobal(sizeof(int) * hs.Length);
		FillMemory(innerCount, hs.Length, 0);
		var innerIndexes2 = (int*)Marshal.AllocHGlobal(sizeof(int) * count);
		FillMemory(innerIndexes2, count, 0);
		Parallel.For(0, count, i =>
		{
			int c;
			lock (lockObj[innerIndexes[i] % lockObj.Length])
				c = innerCount[innerIndexes[i]]++;
			innerIndexes2[i] = c;
		});
		var result = RedStarLinq.EmptyList<(uint[] Group, TSource Key)>(hs.Length);
		Parallel.For(0, hs.Length, i => result[i] = (new uint[innerCount[i]], dicKeys[i]));
		Parallel.For(0, count, i => result[innerIndexes[i]].Group[innerIndexes2[i]] = (uint)i);
		Marshal.FreeHGlobal((IntPtr)innerCount);
		Marshal.FreeHGlobal((IntPtr)innerIndexes2);
		Marshal.FreeHGlobal((IntPtr)innerIndexes);
		return result;
	}

	public static List<(uint[] Group, TSource Key)> PGroup<TSource>(this NList<TSource> source, int tn, G.IEqualityComparer<TSource>? comparer = null) where TSource : unmanaged
	{
		var lockObj = RedStarLinq.FillArray(Environment.ProcessorCount, x => new object());
		var lockObj2 = RedStarLinq.FillArray(Environment.ProcessorCount, x => new object());
		var count = source.Length;
		var innerIndexes = (int*)Marshal.AllocHGlobal(sizeof(int) * count);
		FillMemory(innerIndexes, count, 0);
		ParallelHashSet<TSource> hs = new(comparer);
		Status[tn] = 0;
		StatusMaximum[tn] = count;
		Parallel.For(0, count, i =>
		{
			hs.TryAdd(source[i], out innerIndexes[i]);
			Status[tn]++;
		});
		var dicKeys = hs.ToArray();
		var innerCount = (int*)Marshal.AllocHGlobal(sizeof(int) * hs.Length);
		FillMemory(innerCount, hs.Length, 0);
		var innerIndexes2 = (int*)Marshal.AllocHGlobal(sizeof(int) * count);
		FillMemory(innerIndexes2, count, 0);
		Parallel.For(0, count, i =>
		{
			int c;
			lock (lockObj[innerIndexes[i] % lockObj.Length])
				c = innerCount[innerIndexes[i]]++;
			innerIndexes2[i] = c;
		});
		var result = RedStarLinq.EmptyList<(uint[] Group, TSource Key)>(hs.Length);
		Parallel.For(0, hs.Length, i => result[i] = (new uint[innerCount[i]], dicKeys[i]));
		Parallel.For(0, count, i => result[innerIndexes[i]].Group[innerIndexes2[i]] = (uint)i);
		Marshal.FreeHGlobal((IntPtr)innerCount);
		Marshal.FreeHGlobal((IntPtr)innerIndexes2);
		Marshal.FreeHGlobal((IntPtr)innerIndexes);
		return result;
	}

	public static BitList EncodeEqual(uint lower, uint @base)
	{
		if (lower >= @base)
			throw new InvalidOperationException();
		var bitsCount = BitsCount(@base);
		var threshold = (uint)(1 << bitsCount) - @base;
		var shifted = lower < threshold ? lower : (lower - threshold >> 1) + threshold;
		var result = new BitList(bitsCount - 1, shifted);
		result.Reverse();
		if (lower >= threshold)
			result.Add((lower - threshold & 1) != 0);
		return result;
	}

	public static BitList EncodeFibonacci(uint number)
	{
		BitList bits = default!;
		int i;
		for (i = FibonacciSequence.Length - 1; i >= 0; i--)
		{
			if (FibonacciSequence[i] <= number)
			{
				bits = new(i + 2, false) { [i] = true, [i + 1] = true };
				number -= FibonacciSequence[i];
				break;
			}
		}
		for (i--; i >= 0;)
		{
			if (FibonacciSequence[i] <= number)
			{
				bits[i] = true;
				number -= FibonacciSequence[i];
				i -= 2;
			}
			else
			{
				i--;
			}
		}
		return bits;
	}

	public static void WriteCount(this List<Interval> result, uint count, uint maxT = 31)
	{
		var t = Max(BitsCount(count) - 1, 0);
		result.Add(new((uint)t, maxT));
		var t2 = (uint)1 << Max(t, 1);
		result.Add(new(count - ((t == 0) ? 0 : t2), t2));
	}

	public static void WriteCount(this ShortIntervalList result, uint count, uint maxT = 31)
	{
		var t = Max(BitsCount(count) - 1, 0);
		result.Add(new((uint)t, maxT));
		var t2 = (uint)1 << Max(t, 1);
		result.Add(new(count - ((t == 0) ? 0 : t2), t2));
	}

	public static List<Interval> GetCountList(uint count, uint maxT = 31)
	{
		List<Interval> list = new();
		list.WriteCount(count, maxT);
		return list;
	}

	public static uint GetBaseWithBuffer(uint oldBase) => oldBase + GetBufferInterval(oldBase);
	public static uint GetBufferInterval(uint oldBase) => Max((oldBase + 10) / 20, 1);

	/// <summary>Считает количество бит в числе. Логарифм для этой цели использовать невыгодно, так как это достаточно медленная операция.</summary>
	/// <param name="x">Исходное число.</param>
	/// <returns>Количество бит в числе.</returns>
	public static int BitsCount(uint x)
	{
		var x_ = x;
		var count = 0;
		while (x_ > 0)
		{
			x_ >>= 1;
			count++;
		}
		return count;
	}
}

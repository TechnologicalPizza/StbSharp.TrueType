
namespace StbSharp
{
    public struct CharacterRange
    {
        public static CharacterRange BasicLatin { get; } = new CharacterRange(0x0020, 0x007F);
        public static CharacterRange Latin1Supplement { get; } = new CharacterRange(0x00A0, 0x00FF);
        public static CharacterRange LatinExtendedA { get; } = new CharacterRange(0x0100, 0x017F);
        public static CharacterRange LatinExtendedB { get; } = new CharacterRange(0x0180, 0x024F);
        public static CharacterRange Cyrillic { get; } = new CharacterRange(0x0400, 0x04FF);
        public static CharacterRange CyrillicSupplement { get; } = new CharacterRange(0x0500, 0x052F);
        public static CharacterRange Hiragana { get; } = new CharacterRange(0x3040, 0x309F);
        public static CharacterRange Katakana { get; } = new CharacterRange(0x30A0, 0x30FF);
        public static CharacterRange Greek { get; } = new CharacterRange(0x0370, 0x03FF);
        public static CharacterRange CjkSymbolsAndPunctuation { get; } = new CharacterRange(0x3000, 0x303F);
        public static CharacterRange CjkUnifiedIdeographs { get; } = new CharacterRange(0x4e00, 0x9fff);
        public static CharacterRange HangulCompatibilityJamo { get; } = new CharacterRange(0x3130, 0x318f);
        public static CharacterRange HangulSyllables { get; } = new CharacterRange(0xac00, 0xd7af);

        public int Start { get; }
        public int End { get; }

        public int Size => End - Start + 1;

        public CharacterRange(int start, int end)
        {
            Start = start;
            End = end;
        }

        public CharacterRange(int single) : this(single, single)
        {
        }
    }
}
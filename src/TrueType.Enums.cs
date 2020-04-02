
namespace StbSharp
{
#if !STBSHARP_INTERNAL
    public
#else
    internal
#endif
    unsafe partial class TrueType
    {
        public enum VertexType : byte
        {
            Move = 1,
            Line = 2,
            Curve = 3,
            Cubic = 4
        }

        public enum FontPlatformID
        {
            Unicode = 0,
            Mac = 1,
            ISO = 2,
            Microsoft = 3
        }

        public enum FontUnicodeEncodingID
        {
            Unicode_1_0 = 0,
            Unicode_1_1 = 1,
            ISO_10646 = 2,
            Unicode_2_0_BMP = 3,
            Unicode_2_0_Full = 4
        }

        public enum FontMicrosoftEncodingID
        {
            Symbol = 0,
            Unicode_BMP = 1,
            ShiftJIS = 2,
            Unicode_Full = 10
        }

        public enum FontMacEncodingID
        {
            Roman = 0,
            Arabic = 4,
            Japanese = 1,
            Hebrew = 5,
            ChineseTraditional = 2,
            Greek = 6,
            Korean = 3,
            Russian = 7
        }

        public enum FontMicrosoftLanguageID
        {
            English = 0x0409,
            Italian = 0x0410,
            Chinese = 0x0804,
            Japanese = 0x0411,
            Dutch = 0x0413,
            Korean = 0x0412,
            French = 0x040c,
            Russion = 0x0419,
            German = 0x0407,
            Spanish = 0x0409,
            Hebrew = 0x040d,
            Swedish = 0x041D
        }

        public enum FontMacLanguageID
        {
            English = 0,
            Japanese = 11,
            Arabic = 12,
            Korean = 23,
            Dutch = 4,
            Russion = 32,
            French = 1,
            Spanish = 6,
            German = 2,
            Swedish = 5,
            Hebrew = 10,
            ChineseSimplified = 33,
            Italian = 3,
            ChineseTraditional = 19
        }
    }
}

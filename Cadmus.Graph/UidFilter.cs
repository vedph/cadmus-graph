﻿using System;
using System.Text;
using Fusi.Text.Unicode;

namespace Cadmus.Graph
{
    /// <summary>
    /// UID filter. This is used to adjust a generated UID so that it conforms
    /// to the conventions defined for them.
    /// </summary>
    public static class UidFilter
    {
        #region Unicode segments
        // 0020-024F
        private static readonly int[] _r1 = new[]
        {
            0x0020, 0x0021, 0x0022, 0x0023, 0x0024, 0x0025, 0x0026, 0x0027,
            0x0028, 0x0029, 0x002A, 0x002B, 0x002C, 0x002D, 0x002E, 0x002F,
            0x0030, 0x0031, 0x0032, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037,
            0x0038, 0x0039, 0x003A, 0x003B, 0x003C, 0x003D, 0x003E, 0x003F,
            0x0040, 0x0041, 0x0042, 0x0043, 0x0044, 0x0045, 0x0046, 0x0047,
            0x0048, 0x0049, 0x004A, 0x004B, 0x004C, 0x004D, 0x004E, 0x004F,
            0x0050, 0x0051, 0x0052, 0x0053, 0x0054, 0x0055, 0x0056, 0x0057,
            0x0058, 0x0059, 0x005A, 0x005B, 0x005C, 0x005D, 0x005E, 0x005F,
            0x0060, 0x0061, 0x0062, 0x0063, 0x0064, 0x0065, 0x0066, 0x0067,
            0x0068, 0x0069, 0x006A, 0x006B, 0x006C, 0x006D, 0x006E, 0x006F,
            0x0070, 0x0071, 0x0072, 0x0073, 0x0074, 0x0075, 0x0076, 0x0077,
            0x0078, 0x0079, 0x007A, 0x007B, 0x007C, 0x007D, 0x007E, 0x007F,
            0x0080, 0x0081, 0x0082, 0x0083, 0x0084, 0x0085, 0x0086, 0x0087,
            0x0088, 0x0089, 0x008A, 0x008B, 0x008C, 0x008D, 0x008E, 0x008F,
            0x0090, 0x0091, 0x0092, 0x0093, 0x0094, 0x0095, 0x0096, 0x0097,
            0x0098, 0x0099, 0x009A, 0x009B, 0x009C, 0x009D, 0x009E, 0x009F,
            0x00A0, 0x00A1, 0x00A2, 0x00A3, 0x00A4, 0x00A5, 0x00A6, 0x00A7,
            0x00A8, 0x00A9, 0x00AA, 0x00AB, 0x00AC, 0x00AD, 0x00AE, 0x00AF,
            0x00B0, 0x00B1, 0x00B2, 0x00B3, 0x00B4, 0x00B5, 0x00B6, 0x00B7,
            0x00B8, 0x00B9, 0x00BA, 0x00BB, 0x00BC, 0x00BD, 0x00BE, 0x00BF,
            0x0041, 0x0041, 0x0041, 0x0041, 0x0041, 0x0041, 0x00C6, 0x0043,
            0x0045, 0x0045, 0x0045, 0x0045, 0x0049, 0x0049, 0x0049, 0x0049,
            0x00D0, 0x004E, 0x004F, 0x004F, 0x004F, 0x004F, 0x004F, 0x00D7,
            0x00D8, 0x0055, 0x0055, 0x0055, 0x0055, 0x0059, 0x00DE, 0x00DF,
            0x0061, 0x0061, 0x0061, 0x0061, 0x0061, 0x0061, 0x00E6, 0x0063,
            0x0065, 0x0065, 0x0065, 0x0065, 0x0069, 0x0069, 0x0069, 0x0069,
            0x00F0, 0x006E, 0x006F, 0x006F, 0x006F, 0x006F, 0x006F, 0x00F7,
            0x00F8, 0x0075, 0x0075, 0x0075, 0x0075, 0x0079, 0x00FE, 0x0079,
            0x0041, 0x0061, 0x0041, 0x0061, 0x0041, 0x0061, 0x0043, 0x0063,
            0x0043, 0x0063, 0x0043, 0x0063, 0x0043, 0x0063, 0x0044, 0x0064,
            0x0110, 0x0111, 0x0045, 0x0065, 0x0045, 0x0065, 0x0045, 0x0065,
            0x0045, 0x0065, 0x0045, 0x0065, 0x0047, 0x0067, 0x0047, 0x0067,
            0x0047, 0x0067, 0x0047, 0x0067, 0x0048, 0x0068, 0x0126, 0x0127,
            0x0049, 0x0069, 0x0049, 0x0069, 0x0049, 0x0069, 0x0049, 0x0069,
            0x0049, 0x0131, 0x0132, 0x0133, 0x004A, 0x006A, 0x004B, 0x006B,
            0x0138, 0x004C, 0x006C, 0x004C, 0x006C, 0x004C, 0x006C, 0x013F,
            0x0140, 0x0141, 0x0142, 0x004E, 0x006E, 0x004E, 0x006E, 0x004E,
            0x006E, 0x0149, 0x014A, 0x014B, 0x004F, 0x006F, 0x004F, 0x006F,
            0x004F, 0x006F, 0x0152, 0x0153, 0x0052, 0x0072, 0x0052, 0x0072,
            0x0052, 0x0072, 0x0053, 0x0073, 0x0053, 0x0073, 0x0053, 0x0073,
            0x0053, 0x0073, 0x0054, 0x0074, 0x0054, 0x0074, 0x0166, 0x0167,
            0x0055, 0x0075, 0x0055, 0x0075, 0x0055, 0x0075, 0x0055, 0x0075,
            0x0055, 0x0075, 0x0055, 0x0075, 0x0057, 0x0077, 0x0059, 0x0079,
            0x0059, 0x005A, 0x007A, 0x005A, 0x007A, 0x005A, 0x007A, 0x017F,
            0x0180, 0x0181, 0x0182, 0x0183, 0x0184, 0x0185, 0x0186, 0x0187,
            0x0188, 0x0189, 0x018A, 0x018B, 0x018C, 0x018D, 0x018E, 0x018F,
            0x0190, 0x0191, 0x0192, 0x0193, 0x0194, 0x0195, 0x0196, 0x0197,
            0x0198, 0x0199, 0x019A, 0x019B, 0x019C, 0x019D, 0x019E, 0x019F,
            0x004F, 0x006F, 0x01A2, 0x01A3, 0x01A4, 0x01A5, 0x01A6, 0x01A7,
            0x01A8, 0x01A9, 0x01AA, 0x01AB, 0x01AC, 0x01AD, 0x01AE, 0x0055,
            0x0075, 0x01B1, 0x01B2, 0x01B3, 0x01B4, 0x01B5, 0x01B6, 0x01B7,
            0x01B8, 0x01B9, 0x01BA, 0x01BB, 0x01BC, 0x01BD, 0x01BE, 0x01BF,
            0x01C0, 0x01C1, 0x01C2, 0x01C3, 0x01C4, 0x01C5, 0x01C6, 0x01C7,
            0x01C8, 0x01C9, 0x01CA, 0x01CB, 0x01CC, 0x0041, 0x0061, 0x0049,
            0x0069, 0x004F, 0x006F, 0x0055, 0x0075, 0x0055, 0x0075, 0x0055,
            0x0075, 0x0055, 0x0075, 0x0055, 0x0075, 0x01DD, 0x0041, 0x0061,
            0x0041, 0x0061, 0x00C6, 0x00E6, 0x01E4, 0x01E5, 0x0047, 0x0067,
            0x004B, 0x006B, 0x004F, 0x006F, 0x004F, 0x006F, 0x01B7, 0x0292,
            0x006A, 0x01F1, 0x01F2, 0x01F3, 0x0047, 0x0067, 0x01F6, 0x01F7,
            0x004E, 0x006E, 0x0041, 0x0061, 0x00C6, 0x00E6, 0x00D8, 0x00F8,
            0x0041, 0x0061, 0x0041, 0x0061, 0x0045, 0x0065, 0x0045, 0x0065,
            0x0049, 0x0069, 0x0049, 0x0069, 0x004F, 0x006F, 0x004F, 0x006F,
            0x0052, 0x0072, 0x0052, 0x0072, 0x0055, 0x0075, 0x0055, 0x0075,
            0x0053, 0x0073, 0x0054, 0x0074, 0x021C, 0x021D, 0x0048, 0x0068,
            0x0220, 0x0221, 0x0222, 0x0223, 0x0224, 0x0225, 0x0041, 0x0061,
            0x0045, 0x0065, 0x004F, 0x006F, 0x004F, 0x006F, 0x004F, 0x006F,
            0x004F, 0x006F, 0x0059, 0x0079, 0x0234, 0x0235, 0x0236, 0x0237,
            0x0238, 0x0239, 0x023A, 0x023B, 0x023C, 0x023D, 0x023E, 0x023F,
            0x0240, 0x0241, 0x0242, 0x0243, 0x0244, 0x0245, 0x0246, 0x0247,
            0x0248, 0x0249, 0x024A, 0x024B, 0x024C, 0x024D, 0x024E, 0x024F,
        };
        // 0370-03FF
        private static readonly int[] _r2 = new[]
        {
            0x0370, 0x0371, 0x0372, 0x0373, 0x02B9, 0x0375, 0x0376, 0x0377,
            0x0378, 0x0379, 0x037A, 0x037B, 0x037C, 0x037D, 0x003B, 0x037F,
            0x0380, 0x0381, 0x0382, 0x0383, 0x0384, 0x00A8, 0x0391, 0x00B7,
            0x0395, 0x0397, 0x0399, 0x038B, 0x039F, 0x038D, 0x03A5, 0x03A9,
            0x03B9, 0x0391, 0x0392, 0x0393, 0x0394, 0x0395, 0x0396, 0x0397,
            0x0398, 0x0399, 0x039A, 0x039B, 0x039C, 0x039D, 0x039E, 0x039F,
            0x03A0, 0x03A1, 0x03A2, 0x03A3, 0x03A4, 0x03A5, 0x03A6, 0x03A7,
            0x03A8, 0x03A9, 0x0399, 0x03A5, 0x03B1, 0x03B5, 0x03B7, 0x03B9,
            0x03C5, 0x03B1, 0x03B2, 0x03B3, 0x03B4, 0x03B5, 0x03B6, 0x03B7,
            0x03B8, 0x03B9, 0x03BA, 0x03BB, 0x03BC, 0x03BD, 0x03BE, 0x03BF,
            0x03C0, 0x03C1, 0x03C2, 0x03C3, 0x03C4, 0x03C5, 0x03C6, 0x03C7,
            0x03C8, 0x03C9, 0x03B9, 0x03C5, 0x03BF, 0x03C5, 0x03C9, 0x03CF,
            0x03D0, 0x03D1, 0x03D2, 0x03D2, 0x03D2, 0x03D5, 0x03D6, 0x03D7,
            0x03D8, 0x03D9, 0x03DA, 0x03DB, 0x03DC, 0x03DD, 0x03DE, 0x03DF,
            0x03E0, 0x03E1, 0x03E2, 0x03E3, 0x03E4, 0x03E5, 0x03E6, 0x03E7,
            0x03E8, 0x03E9, 0x03EA, 0x03EB, 0x03EC, 0x03ED, 0x03EE, 0x03EF,
            0x03F0, 0x03F1, 0x03F2, 0x03F3, 0x03F4, 0x03F5, 0x03F6, 0x03F7,
            0x03F8, 0x03F9, 0x03FA, 0x03FB, 0x03FC, 0x03FD, 0x03FE, 0x03FF,
        };
        // 1E00-1EFF
        private static readonly int[] _r3 = new[]
        {
            0x0041, 0x0061, 0x0042, 0x0062, 0x0042, 0x0062, 0x0042, 0x0062,
            0x0043, 0x0063, 0x0044, 0x0064, 0x0044, 0x0064, 0x0044, 0x0064,
            0x0044, 0x0064, 0x0044, 0x0064, 0x0045, 0x0065, 0x0045, 0x0065,
            0x0045, 0x0065, 0x0045, 0x0065, 0x0045, 0x0065, 0x0046, 0x0066,
            0x0047, 0x0067, 0x0048, 0x0068, 0x0048, 0x0068, 0x0048, 0x0068,
            0x0048, 0x0068, 0x0048, 0x0068, 0x0049, 0x0069, 0x0049, 0x0069,
            0x004B, 0x006B, 0x004B, 0x006B, 0x004B, 0x006B, 0x004C, 0x006C,
            0x004C, 0x006C, 0x004C, 0x006C, 0x004C, 0x006C, 0x004D, 0x006D,
            0x004D, 0x006D, 0x004D, 0x006D, 0x004E, 0x006E, 0x004E, 0x006E,
            0x004E, 0x006E, 0x004E, 0x006E, 0x004F, 0x006F, 0x004F, 0x006F,
            0x004F, 0x006F, 0x004F, 0x006F, 0x0050, 0x0070, 0x0050, 0x0070,
            0x0052, 0x0072, 0x0052, 0x0072, 0x0052, 0x0072, 0x0052, 0x0072,
            0x0053, 0x0073, 0x0053, 0x0073, 0x0053, 0x0073, 0x0053, 0x0073,
            0x0053, 0x0073, 0x0054, 0x0074, 0x0054, 0x0074, 0x0054, 0x0074,
            0x0054, 0x0074, 0x0055, 0x0075, 0x0055, 0x0075, 0x0055, 0x0075,
            0x0055, 0x0075, 0x0055, 0x0075, 0x0056, 0x0076, 0x0056, 0x0076,
            0x0057, 0x0077, 0x0057, 0x0077, 0x0057, 0x0077, 0x0057, 0x0077,
            0x0057, 0x0077, 0x0058, 0x0078, 0x0058, 0x0078, 0x0059, 0x0079,
            0x005A, 0x007A, 0x005A, 0x007A, 0x005A, 0x007A, 0x0068, 0x0074,
            0x0077, 0x0079, 0x1E9A, 0x017F, 0x1E9C, 0x1E9D, 0x1E9E, 0x1E9F,
            0x0041, 0x0061, 0x0041, 0x0061, 0x0041, 0x0061, 0x0041, 0x0061,
            0x0041, 0x0061, 0x0041, 0x0061, 0x0041, 0x0061, 0x0041, 0x0061,
            0x0041, 0x0061, 0x0041, 0x0061, 0x0041, 0x0061, 0x0041, 0x0061,
            0x0045, 0x0065, 0x0045, 0x0065, 0x0045, 0x0065, 0x0045, 0x0065,
            0x0045, 0x0065, 0x0045, 0x0065, 0x0045, 0x0065, 0x0045, 0x0065,
            0x0049, 0x0069, 0x0049, 0x0069, 0x004F, 0x006F, 0x004F, 0x006F,
            0x004F, 0x006F, 0x004F, 0x006F, 0x004F, 0x006F, 0x004F, 0x006F,
            0x004F, 0x006F, 0x004F, 0x006F, 0x004F, 0x006F, 0x004F, 0x006F,
            0x004F, 0x006F, 0x004F, 0x006F, 0x0055, 0x0075, 0x0055, 0x0075,
            0x0055, 0x0075, 0x0055, 0x0075, 0x0055, 0x0075, 0x0055, 0x0075,
            0x0055, 0x0075, 0x0059, 0x0079, 0x0059, 0x0079, 0x0059, 0x0079,
            0x0059, 0x0079, 0x1EFA, 0x1EFB, 0x1EFC, 0x1EFD, 0x1EFE, 0x1EFF,
        };
        // 1F00-1FFF
        private static readonly int[] _r4 = new[]
        {
            0x03B1, 0x03B1, 0x03B1, 0x03B1, 0x03B1, 0x03B1, 0x03B1, 0x03B1,
            0x0391, 0x0391, 0x0391, 0x0391, 0x0391, 0x0391, 0x0391, 0x0391,
            0x03B5, 0x03B5, 0x03B5, 0x03B5, 0x03B5, 0x03B5, 0x1F16, 0x1F17,
            0x0395, 0x0395, 0x0395, 0x0395, 0x0395, 0x0395, 0x1F1E, 0x1F1F,
            0x03B7, 0x03B7, 0x03B7, 0x03B7, 0x03B7, 0x03B7, 0x03B7, 0x03B7,
            0x0397, 0x0397, 0x0397, 0x0397, 0x0397, 0x0397, 0x0397, 0x0397,
            0x03B9, 0x03B9, 0x03B9, 0x03B9, 0x03B9, 0x03B9, 0x03B9, 0x03B9,
            0x0399, 0x0399, 0x0399, 0x0399, 0x0399, 0x0399, 0x0399, 0x0399,
            0x03BF, 0x03BF, 0x03BF, 0x03BF, 0x03BF, 0x03BF, 0x1F46, 0x1F47,
            0x039F, 0x039F, 0x039F, 0x039F, 0x039F, 0x039F, 0x1F4E, 0x1F4F,
            0x03C5, 0x03C5, 0x03C5, 0x03C5, 0x03C5, 0x03C5, 0x03C5, 0x03C5,
            0x1F58, 0x03A5, 0x1F5A, 0x03A5, 0x1F5C, 0x03A5, 0x1F5E, 0x03A5,
            0x03C9, 0x03C9, 0x03C9, 0x03C9, 0x03C9, 0x03C9, 0x03C9, 0x03C9,
            0x03A9, 0x03A9, 0x03A9, 0x03A9, 0x03A9, 0x03A9, 0x03A9, 0x03A9,
            0x03B1, 0x03B1, 0x03B5, 0x03B5, 0x03B7, 0x03B7, 0x03B9, 0x03B9,
            0x03BF, 0x03BF, 0x03C5, 0x03C5, 0x03C9, 0x03C9, 0x1F7E, 0x1F7F,
            0x03B1, 0x03B1, 0x03B1, 0x03B1, 0x03B1, 0x03B1, 0x03B1, 0x03B1,
            0x0391, 0x0391, 0x0391, 0x0391, 0x0391, 0x0391, 0x0391, 0x0391,
            0x03B7, 0x03B7, 0x03B7, 0x03B7, 0x03B7, 0x03B7, 0x03B7, 0x03B7,
            0x0397, 0x0397, 0x0397, 0x0397, 0x0397, 0x0397, 0x0397, 0x0397,
            0x03C9, 0x03C9, 0x03C9, 0x03C9, 0x03C9, 0x03C9, 0x03C9, 0x03C9,
            0x03A9, 0x03A9, 0x03A9, 0x03A9, 0x03A9, 0x03A9, 0x03A9, 0x03A9,
            0x03B1, 0x03B1, 0x03B1, 0x03B1, 0x03B1, 0x1FB5, 0x03B1, 0x03B1,
            0x0391, 0x0391, 0x0391, 0x0391, 0x0391, 0x1FBD, 0x03B9, 0x1FBF,
            0x1FC0, 0x00A8, 0x03B7, 0x03B7, 0x03B7, 0x1FC5, 0x03B7, 0x03B7,
            0x0395, 0x0395, 0x0397, 0x0397, 0x0397, 0x1FBF, 0x1FBF, 0x1FBF,
            0x03B9, 0x03B9, 0x03B9, 0x03B9, 0x1FD4, 0x1FD5, 0x03B9, 0x03B9,
            0x0399, 0x0399, 0x0399, 0x0399, 0x1FDC, 0x1FFE, 0x1FFE, 0x1FFE,
            0x03C5, 0x03C5, 0x03C5, 0x03C5, 0x03C1, 0x03C1, 0x03C5, 0x03C5,
            0x03A5, 0x03A5, 0x03A5, 0x03A5, 0x03A1, 0x00A8, 0x00A8, 0x0060,
            0x1FF0, 0x1FF1, 0x03C9, 0x03C9, 0x03C9, 0x1FF5, 0x03C9, 0x03C9,
            0x039F, 0x039F, 0x03A9, 0x03A9, 0x03A9, 0x00B4, 0x1FFE, 0x1FFF,
        };
        #endregion

        private static readonly Lazy<UniData> _ud = new(() => new UniData());

        /// <summary>
        /// Gets the Unicode data helper used by this filter.
        /// </summary>
        public static UniData UniData => _ud.Value;

        /// <summary>
        /// Gets the "segmental" counterpart of the specified Unicode character.
        /// </summary>
        /// <param name="c">The character.</param>
        /// <returns>Segment or 0.</returns>
        public static char GetSegment(char c)
        {
            // use optimized ranges when possible
            if (c >= 0x0020 && c <= 0x024F) return (char)_r1[(int)c - 0x0020];
            if (c >= 0x0370 && c <= 0x03FF) return (char)_r2[(int)c - 0x0370];
            if (c >= 0x1E00 && c <= 0x1EFF) return (char)_r3[(int)c - 0x1E00];
            if (c >= 0x1F00 && c <= 0x1FFF) return (char)_r4[(int)c - 0x1F00];

            return _ud.Value.GetSegment(c, true);
        }

        /// <summary>
        /// Apply this UID filter to the specified UID value.
        /// This replaces whitespace with <c>_</c>, preserves only letters
        /// (lowercased and without diacritics), digits, and characters
        /// <c>-_#/&%=.?</c>. It also ensures that the UID is not empty,
        /// in this case replacing it with <c>_</c>.
        /// If the UID starts with <c>!</c>, no filters are applied except
        /// for removing the initial <c>!</c>.
        /// </summary>
        /// <param name="uid">the UID to apply this filter to.</param>
        /// <returns>Filtered UID.</returns>
        /// <exception cref="ArgumentNullException">uid</exception>
        public static string Apply(string uid)
        {
            if (uid is null) throw new ArgumentNullException(nameof(uid));

            // ensure the UID is not empty
            if (uid.Length == 0) return "_";

            // honor filter disable directive (initial !)
            if (uid[0] == '!')
            {
                uid = uid[1..];
                return uid.Length > 0? uid : "_";
            }

            // filter
            StringBuilder sb = new(uid.Length);
            foreach (char c in uid)
            {
                switch (c)
                {
                    case ':':
                    case '_':
                    case '-':
                    case '#':
                    case '/':
                    case '&':
                    case '%':
                    case '=':
                    case '.':
                    case '?':
                        sb.Append(c);
                        break;
                    default:
                        if (char.IsLetter(c))
                        {
                            // TODO optimize
                            sb.Append(char.ToLowerInvariant(
                                _ud.Value.GetSegment(c, true)));
                            break;
                        }
                        if (c >= '0' && c <= '9')
                        {
                            sb.Append(c);
                            break;
                        }
                        if (char.IsWhiteSpace(c)) sb.Append('_');
                        break;
                }
            }
            return sb.ToString();
        }
    }
}

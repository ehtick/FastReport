﻿using FastReport.Format;
using FastReport.Utils;
using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;


namespace FastReport.Export
{
    /// <summary>
    /// For internal use only.
    /// </summary>
    public static class ExportUtils
    {
        internal const string XCONV = "0123456789ABCDEF";
        private const int BASE = 65521;
        private const int NMAX = 5552;

        internal enum CRLF
        {
            html,
            xml,
            odt
        };

        /// <summary>
        /// Gets current page width.
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public static float GetPageWidth(ReportPage page)
        {
            if (page.UnlimitedWidth)
                return page.UnlimitedWidthValue / Units.Millimeters;
            else
                return page.PaperWidth;
        }

        /// <summary>
        /// Gets current page height.
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public static float GetPageHeight(ReportPage page)
        {
            if (page.UnlimitedHeight)
                return page.UnlimitedHeightValue / Units.Millimeters;
            else
                return page.PaperHeight;
        }


        private static readonly NumberFormatInfo _provider;
        public static string FloatToString(double value, int digits = 2)
        {
            return Convert.ToString(Math.Round(value, digits), _provider);
        }

        internal static bool ParseTextToDecimal(string text, FormatBase format, out decimal value)
        {
            value = 0;
            if (format is NumberFormat)
            {
                return decimal.TryParse(text, NumberStyles.Number, (format as NumberFormat).GetNumberFormatInfo(), out value);
            }
            else if (format is CurrencyFormat)
            {
                return decimal.TryParse(text, NumberStyles.Currency, (format as CurrencyFormat).GetNumberFormatInfo(), out value);
            }
            else if (format is CustomFormat && !(format is DateFormat))
            {
                return decimal.TryParse(text, out value);
            }
            return false;
        }

        internal static bool ParseTextToDateTime(string text, FormatBase format, out DateTime value)
        {
            value = DateTime.MinValue;
            if (format is DateFormat)
                return DateTime.TryParse(text, CultureInfo.CurrentCulture.DateTimeFormat, DateTimeStyles.None, out value);
            return false;
        }

        internal static bool ParseTextToPercent(string text, FormatBase format, out decimal value)
        {
            value = 0;
            if (format is PercentFormat)
            {
                bool returned = decimal.TryParse(text.Replace("%", ""), out value);
                value /= 100;
                return returned;
            }
            return false;
        }

        internal static string GetExcelFormatSpecifier(FormatBase format, bool useLocale = false, bool currencyToAccounting = false)
        {
            if (format is CurrencyFormat)
            {
                NumberFormatInfo f = (format as CurrencyFormat).GetNumberFormatInfo();

                if (useLocale)
                {
                    (format as CurrencyFormat).UseLocale = true;
                    f = (format as CurrencyFormat).GetNumberFormatInfo();
                    f.CurrencyDecimalDigits = CultureInfo.CurrentCulture.NumberFormat.CurrencyDecimalDigits;
                }

                string fm_str = "#,##0";
                if (f.CurrencyDecimalDigits > 0)
                {
                    fm_str += ".";// f.DecimalSeparator;
                    for (int i = 0; i < f.CurrencyDecimalDigits; i++)
                        fm_str += "0";
                }
                string currency_symbol = "&quot;" + f.CurrencySymbol + "&quot;";
                string positive_pattern = "";
                string negative_pattern = "";

                switch (f.CurrencyPositivePattern)
                {
                    case 0: positive_pattern = currency_symbol + fm_str; break;       // $n 
                    case 1: positive_pattern = fm_str + currency_symbol; break;       // n$
                    case 2: positive_pattern = currency_symbol + " " + fm_str; break; // $ n
                    case 3: positive_pattern = fm_str + " " + currency_symbol; break; // n $
                }

                switch (f.CurrencyNegativePattern)
                {
                    case 0: negative_pattern = "(" + currency_symbol + fm_str + ")"; break;        // ($n)
                    case 1: negative_pattern = "-" + currency_symbol + fm_str; break;              // -$n
                    case 2: negative_pattern = currency_symbol + "-" + fm_str; break;              // $-n
                    case 3: negative_pattern = currency_symbol + fm_str + "-"; break;              // $n-
                    case 4: negative_pattern = "(" + currency_symbol + fm_str + ")"; break;        // (n$)
                    case 5: negative_pattern = "-" + fm_str + currency_symbol; break;              // -n$
                    case 6: negative_pattern = fm_str + "-" + currency_symbol; break;              // n-$
                    case 7: negative_pattern = fm_str + currency_symbol + "-"; break;              // n$-
                    case 8: negative_pattern = "-" + fm_str + " " + currency_symbol; break;        // -n $
                    case 9: negative_pattern = "-" + currency_symbol + " " + fm_str; break;        // -$ n
                    case 10: negative_pattern = fm_str + " " + currency_symbol + "-"; break;       // n $-
                    case 11: negative_pattern = currency_symbol + " " + fm_str + "-"; break;       // $ n-
                    case 12: negative_pattern = currency_symbol + " -" + fm_str; break;            // $ -n
                    case 13: negative_pattern = fm_str + "- " + currency_symbol; break;            // n- $
                    case 14: negative_pattern = "(" + currency_symbol + " " + fm_str + ")"; break; // ($ n)
                    case 15: negative_pattern = "(" + fm_str + " " + currency_symbol + ")"; break; // (n $)
                }

                if (currencyToAccounting)
                {
                    string financeSymbol = "";

                    switch (f.CurrencySymbol)
                    {
                        case "₽": financeSymbol = "[$₽-419]"; break;
                        case "$": financeSymbol = "[$$-409]"; break;
                        case "\u20AC": financeSymbol = "[$€-2]"; break;
                        case "\u00A3": financeSymbol = "[$£-809]"; break;
                        case "\u20B9": financeSymbol = "[$₹-4009]"; break;
                    }

                    switch (f.CurrencyPositivePattern)
                    {
                        case 0: positive_pattern = @"_-* " + financeSymbol + fm_str + "_-;"; break;       // $n 
                        case 1: positive_pattern = @"_-* " + fm_str + financeSymbol + "_-;"; break;       // n$
                        case 2: positive_pattern = @"_-* " + financeSymbol + " " + fm_str + "_-;"; break; // $ n
                        case 3: positive_pattern = @"_-* " + fm_str + " " + financeSymbol + "_-;"; break;  // n $
                    }

                    switch (f.CurrencyNegativePattern)
                    {
                        case 0: negative_pattern = @"_-* " + "(" + financeSymbol + fm_str + ")" + "_-;"; break;        // ($n)
                        case 1: negative_pattern = @"_-* " + "-" + financeSymbol + fm_str + "_-;"; break;              // -$n
                        case 2: negative_pattern = @"_-* " + financeSymbol + "-" + fm_str + "_-;"; break;              // $-n
                        case 3: negative_pattern = @"_-* " + financeSymbol + fm_str + "-" + "_-;"; break;              // $n-
                        case 4: negative_pattern = @"_-* " + "(" + fm_str + financeSymbol + ")" + "_-;"; break;        // (n$)
                        case 5: negative_pattern = @"_-* " + "-" + fm_str + financeSymbol + "_-;"; break;              // -n$
                        case 6: negative_pattern = @"_-* " + fm_str + "-" + financeSymbol + "_-;"; break;              // n-$
                        case 7: negative_pattern = @"_-* " + fm_str + financeSymbol + "-" + "_-;"; break;              // n$-
                        case 8: negative_pattern = @"_-* " + "-" + fm_str + " " + financeSymbol + "_-;"; break;        // -n $
                        case 9: negative_pattern = @"_-* " + "-" + financeSymbol + " " + fm_str + "_-;"; break;        // -$ n
                        case 10: negative_pattern = @"_-* " + fm_str + " " + financeSymbol + "-" + "_-;"; break;       // n $-
                        case 11: negative_pattern = @"_-* " + financeSymbol + " " + fm_str + "-" + "_-;"; break;       // $ n-
                        case 12: negative_pattern = @"_-* " + financeSymbol + " " + "-" + fm_str + "_-;"; break;       // $ -n
                        case 13: negative_pattern = @"_-* " + fm_str + "- " + financeSymbol + "_-;"; break;            // n- $
                        case 14: negative_pattern = @"_-* " + "(" + financeSymbol + " " + fm_str + ")" + "_-;"; break; // ($ n)
                        case 15: negative_pattern = @"_-* " + "(" + fm_str + " " + financeSymbol + ")" + "_-;"; break; // (n $)
                    }

                    string financeFormat_Options = @"_-* &quot;-&quot;??\ " + financeSymbol + "_-;_-@_-";

                    return positive_pattern + negative_pattern + financeFormat_Options;
                }
                return positive_pattern + ";" + negative_pattern;
            }
            else if (format is NumberFormat)
            {
                NumberFormatInfo f = (format as NumberFormat).GetNumberFormatInfo();

                if (useLocale)
                {
                    (format as NumberFormat).UseLocale = true;
                    f = (format as NumberFormat).GetNumberFormatInfo();
                    f.CurrencyDecimalDigits = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalDigits;
                }

                string fm_str = "#,##0";
                if (f.NumberDecimalDigits > 0)
                {
                    fm_str += ".";// f.DecimalSeparator;
                    for (int i = 0; i < f.NumberDecimalDigits; i++)
                        fm_str += "0";
                }
                string positive_pattern = "";
                string negative_pattern = "";
                positive_pattern = fm_str;

                switch (f.NumberNegativePattern)
                {
                    case 0: negative_pattern = "(" + fm_str + ")"; break; // (n)
                 // case 1: negative_pattern = "-" + fm_str; break;       // -n
                    case 2: negative_pattern = "- " + fm_str; break;      // - n
                    case 3: negative_pattern = fm_str + "-"; break;       // n-
                    case 4: negative_pattern = fm_str + " -"; break;      // n -
                }
                string semicolon = !String.IsNullOrEmpty(negative_pattern) ? ";" : "";
                return positive_pattern + semicolon + negative_pattern;
            }
            else if (format is DateFormat)
            {
                string parentalCase = CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "ru" ? "[$-419]" : "";
                switch ((format as DateFormat).Format)
                {
                    case "d": return DateTimeFormatInfo.CurrentInfo.ShortDatePattern + ";@";
                    case "D": return "[$-F800]" + "dddd, mmmm dd, yyyy";
                    case "f": return parentalCase + (DateTimeFormatInfo.CurrentInfo.LongDatePattern + " " + DateTimeFormatInfo.CurrentInfo.ShortTimePattern).Replace("tt", "AM/PM") + ";@";
                    case "F": return parentalCase + DateTimeFormatInfo.CurrentInfo.FullDateTimePattern.Replace("tt", "AM/PM") + ";@";
                    case "MMMM yyyy": return parentalCase + DateTimeFormatInfo.CurrentInfo.YearMonthPattern + ";@";
                    default: return (format as DateFormat).Format.Replace("tt", "AM/PM") + ";@";
                }
            }
            else if (format is PercentFormat)
            {
                string pattern = "0";

                if (useLocale)
                {
                    (format as PercentFormat).UseLocale = true;
                    (format as PercentFormat).DecimalDigits = CultureInfo.CurrentCulture.NumberFormat.PercentDecimalDigits;
                }

                if ((format as PercentFormat).DecimalDigits > 0)
                {
                    pattern += ".";
                    for (int i = 0; i < (format as PercentFormat).DecimalDigits; i++)
                        pattern += "0";
                }
                return pattern + "%";
            }

            return "";
        }

        internal static string HTMLColor(Color color)
        {
            if (color.A < 255)
            {
                string alphaValue = (color.A / 255.0).ToString("0.00", INVARIANT_CULTURE);
                return $"rgba({color.R}, {color.G}, {color.B}, {alphaValue})";
            }
            return $"rgb({color.R}, {color.G}, {color.B})";
        }

        internal static string HTMLColorCode(Color color)
        {
            return String.Join(String.Empty, new String[] {
                "#",
                color.R.ToString("X2"),
                color.G.ToString("X2"),
                color.B.ToString("X2")
            });
        }

        internal static string ByteToHex(byte Byte)
        {
            char[] s = new char[2];
            s[0] = XCONV[(Byte >> 4)];
            s[1] = XCONV[(Byte & 0xF)];
            return new String(s);
        }

        internal static string UInt16Tohex(UInt16 word)
        {
            FastString sb = new FastString(4);
            return sb.Append(ByteToHex((byte)((word >> 8) & 0xFF))).Append(ByteToHex((byte)(word & 0xFF))).ToString();
        }

        internal static string TruncReturns(string Str)
        {
            if (Str.EndsWith("\r\n"))
                return Str.Substring(0, Str.Length - 2);
            else
                return Str;
        }

        internal static FastString HtmlString(string text)
        {
            return HtmlString(text, TextRenderType.Default);
        }

        internal static FastString HtmlString(string text, TextRenderType textRenderType, CRLF crlf, bool excel2007, string fontSize = "13px;")
        {
            text = text.Replace('\v', '\n');
            if (crlf == CRLF.html)
                text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            FastString Result = new FastString(text.Length);
            int len = text.Length;
            int lineBreakCount = 0;

            if (textRenderType == TextRenderType.HtmlTags)
            {
                const string wingdings = "<font face=\"Wingdings\">";
                const string webdings = "<font face=\"Webdings\">";
                int ind1 = 0, ind2 = 0;
                if (text.Contains(wingdings))
                {
                    ind1 = text.IndexOf(wingdings) + wingdings.Length;
                    ind2 = text.IndexOf('<', ind1);
                    text = text.Substring(0, ind1) +
                        WingdingsToUnicodeConverter.Convert(text.Substring(ind1, ind2 - ind1)) +
                        text.Substring(ind2, text.Length - ind2);
                }
                else if (text.Contains(webdings))
                {
                    ind1 = text.IndexOf(webdings) + webdings.Length;
                    ind2 = text.IndexOf('<', ind1);
                    text = text.Substring(0, ind1) +
                        WingdingsToUnicodeConverter.Convert(text.Substring(ind1, ind2 - ind1)) +
                        text.Substring(ind2, text.Length - ind2);
                }
            }

            for (int i = 0; i < len; i++)
            {
                if (crlf != CRLF.xml && crlf != CRLF.odt && text[i] == ' ' && (text.Length == 1 ||
                     (i < (len - 1) && text[i + 1] == ' ') ||
                     (i > 0 && text[i - 1] == ' ')
                     || i == len - 1))
                {
                    Result.Append("&nbsp;");
                }
                else if (text[i] == '<' && textRenderType == TextRenderType.HtmlTags && crlf == CRLF.odt)
                    i += text.IndexOf('>', i) - i;
                else if ((i < text.Length - 1 && text[i] == '\r' && text[i + 1] == '\n') || (crlf == CRLF.html && text[i] == '\n'))
                {
                    if (crlf == CRLF.xml)
                        Result.Append("&#10;");
                    else if (crlf == CRLF.odt)
                        Result.Append("<text:line-break />");
                    else
                    {
                        if (i == 0 && text[i] == '\n')
                            Result.Append($"<p style=\"margin-top:{fontSize}margin-bottom:0px\"></p>");

                        if (lineBreakCount == 0)
                            Result.Append("<p style=\"margin-top:0px;margin-bottom:0px;\"></p>");
                        else
                            Result.Append($"<p style=\"margin-top:0px;height:{fontSize}margin-bottom:0px\"></p>");
                        lineBreakCount++;
                    }

                    if (crlf != CRLF.html)
                        i++;
                }
                else
                {
                    lineBreakCount = 0;
                    if (text[i] == '\t' && crlf == CRLF.odt)
                        Result.Append("<text:tab/>");
                    else if (text[i] == ' ' && crlf == CRLF.odt)
                    {
                        int spaces = 1;
                        while (i < text.Length - 1)
                        {
                            if (text[i + 1] == ' ')
                            {
                                i++;
                                spaces++;
                            }
                            else
                                break;
                        }
                        Result.Append("<text:s text:c=\"" + spaces + "\"/>");
                    }
                    else if (text[i] == '\\')
                        Result.Append("&#92;");
                    else if (text[i] == '~' && !excel2007 && crlf != CRLF.odt)
                        Result.Append("&tilde;");
                    else if (text[i] == '€' && !excel2007 && crlf != CRLF.odt)
                        Result.Append("&euro;");
                    else if (text[i] == '‹' && !excel2007 && crlf != CRLF.odt)
                        Result.Append("&lsaquo;");
                    else if (text[i] == '›' && !excel2007 && crlf != CRLF.odt)
                        Result.Append("&rsaquo;");
                    else if (text[i] == 'ˆ' && !excel2007 && crlf != CRLF.odt)
                        Result.Append("&circ;");
                    else if (text[i] == '&' && textRenderType == TextRenderType.Default)
                        Result.Append("&amp;");
                    else if (text[i] == '"' && textRenderType == TextRenderType.Default)
                        Result.Append("&quot;");
                    else if (text[i] == '<' && textRenderType == TextRenderType.Default)
                        Result.Append("&lt;");
                    else if (text[i] == '>' && textRenderType == TextRenderType.Default)
                        Result.Append("&gt;");
                    else if (text[i] == '\t' && excel2007)
                        Result.Append("&#9;");
                    else
                        Result.Append(text[i]);
                }
            }
            return Result;
        }

        internal static string QuotedPrintable(byte[] Values)
        {
            FastString sb = new FastString((int)(Values.Length * 1.3));
            int length = 0;
            foreach (byte c in Values)
            {
                if (length > 73)
                {
                    length = 0;
                    sb.Append('=').AppendLine();
                }
                if (c < 9 || c == 61 || c > 126)
                {
                    sb.Append('=').Append(XCONV[c >> 4]).Append(XCONV[c & 0xF]);
                    length += 3;
                }
                else
                {
                    sb.Append((char)c);
                    length++;
                }
            }
            return sb.ToString();
        }

        public static FastString HtmlString(string text, TextRenderType textRenderType, string fontSize = "13px;")
        {
            return HtmlString(text, textRenderType, CRLF.html, false, fontSize);
        }

        internal static string XmlString(string Str, TextRenderType textRenderType)
        {
            return HtmlString(Str, textRenderType, CRLF.xml, false).ToString();
        }

        internal static string Excel2007String(string Str, TextRenderType textRenderType)
        {
            return HtmlString(Str, textRenderType, CRLF.xml, true).ToString();
        }

        internal static string OdtString(string Str, TextRenderType textRenderType)
        {
            return HtmlString(Str, textRenderType, CRLF.odt, false).ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="Value"></param>
        /// <returns></returns>
        internal static string HtmlURL(string Value)
        {
            FastString Result = new FastString();
#if DOTNET_4
            foreach (char c in Value)
            {
                if (!char.IsLetterOrDigit(c) && !char.IsPunctuation(c))
                    Result.Append("%").Append(Convert.ToInt32(c).ToString("x"));
                else
                    Result.Append(c);
            }
#else
            for (int i = 0; i < Value.Length; i++)
            {
                switch (Value[i])
                {
                    case '\\':
                        Result.Append("/");
                        break;
                    case '&':
                    case '<':
                    case '>':
                    case '{':
                    case '}':
                    case ';':
                    case '?':
                    case ' ':
                    case '\'':
                    case '"':
                        Result.Append("%" + ExportUtils.ByteToHex((byte)Value[i]));
                        break;
                    default:
                        Result.Append(Value[i]);
                        break;
                }
            }
#endif
            return Result.ToString();
        }

        internal static void Write(Stream stream, string value)
        {
            byte[] buf = Encoding.UTF8.GetBytes(value);
            stream.Write(buf, 0, buf.Length);
        }

        internal static void WriteLn(Stream stream, string value)
        {
            byte[] buf = Encoding.UTF8.GetBytes(value);
            stream.Write(buf, 0, buf.Length);
            stream.WriteByte(13);
            stream.WriteByte(10);
        }

        internal static void Write(Stream stream, byte value)
        {
            stream.WriteByte(value);
        }

        internal static void Write(Stream stream, StringBuilder value)
        {
            byte[] buf = Encoding.UTF8.GetBytes(value.ToString());
            stream.Write(buf, 0, buf.Length);
        }

        internal static void WriteLn(Stream stream, StringBuilder value)
        {
            byte[] buf = Encoding.UTF8.GetBytes(value.ToString());
            stream.Write(buf, 0, buf.Length);
            stream.WriteByte(13);
            stream.WriteByte(10);
        }

        internal static void ZLibDeflate(Stream src, Stream dst)
        {
            dst.WriteByte(0x78);
            dst.WriteByte(0xDA);
            src.Position = 0;
            long adler = 1L;
            using (DeflateStream compressor = new DeflateStream(dst, CompressionMode.Compress, true))
            {
                const int bufflength = 2048;
                byte[] buff = new byte[bufflength];
                int i;
                while ((i = src.Read(buff, 0, bufflength)) > 0)
                {
                    adler = Adler32(adler, buff, 0, i);
                    compressor.Write(buff, 0, i);
                }
            }
            dst.WriteByte((byte)(adler >> 24 & 0xFF));
            dst.WriteByte((byte)(adler >> 16 & 0xFF));
            dst.WriteByte((byte)(adler >> 8 & 0xFF));
            dst.WriteByte((byte)(adler & 0xFF));
        }

        internal static long Adler32(long adler, byte[] buf, int index, int len)
        {
            if (buf == null) { return 1L; }

            long s1 = adler & 0xffff;
            long s2 = (adler >> 16) & 0xffff;
            int k;

            while (len > 0)
            {
                k = len < NMAX ? len : NMAX;
                len -= k;
                while (k >= 16)
                {
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    s1 += buf[index++] & 0xff; s2 += s1;
                    k -= 16;
                }
                if (k != 0)
                {
                    do
                    {
                        s1 += buf[index++] & 0xff;
                        s2 += s1;
                    }
                    while (--k != 0);
                }
                s1 %= BASE;
                s2 %= BASE;
            }
            return (s2 << 16) | s1;
        }

        internal static string ReverseString(string str)
        {
            FastString result = new FastString(str.Length);
            int i, j;
            for (j = 0, i = str.Length - 1; i >= 0; i--, j++)
                result.Append(str[i]);
            return result.ToString();
        }

        internal static string StrToHex(string s)
        {
            FastString sb = new FastString(s.Length * 2);
            foreach (char c in s)
                sb.Append(((byte)c).ToString("X2"));
            return sb.ToString();
        }

        internal static FastString StrToHex2(string s)
        {
            FastString sb = new FastString(s.Length * 2);
            foreach (char c in s)
                sb.Append(((UInt16)c).ToString("X4"));
            return sb;
        }

        internal static string GetID()
        {
            return SystemFake.Guid.NewGuid().ToString();
        }

        internal static byte[] StringToByteArray(string source)
        {
            byte[] result = new byte[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = (byte)source[i];
            return result;
        }

        internal static byte[] StringTo2ByteArray(string source)
        {
            byte[] result = new byte[source.Length * 2];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = (byte)(source[i] >> 8);
                result[i + 1] = (byte)source[i];
            }
            return result;
        }

        internal static string StringFromByteArray(byte[] array)
        {
            FastString result = new FastString(array.Length);
            foreach (byte b in array)
                result.Append((char)b);
            return result.ToString();
        }

        internal static Color GetColorFromFill(FillBase Fill)
        {
            if (Fill is SolidFill)
                return (Fill as SolidFill).Color;
            else if (Fill is GlassFill)
                return (Fill as GlassFill).Color;
            else if (Fill is HatchFill)
                return (Fill as HatchFill).BackColor;
            else if (Fill is PathGradientFill)
                return (Fill as PathGradientFill).CenterColor;
            else if (Fill is LinearGradientFill)
                return GetMiddleColor((Fill as LinearGradientFill).StartColor, (Fill as LinearGradientFill).EndColor);
            else
                return Color.White;
        }

        private static Color GetMiddleColor(Color color1, Color color2)
        {
            return Color.FromArgb(255,
                (color1.R + color2.R) / 2,
                (color1.G + color2.G) / 2,
                (color1.B + color2.B) / 2);
        }

        internal static string GetRFCDate(DateTime datetime)
        {
            FastString sb = new FastString();
            sb.AppendFormat("{0:R}", datetime);
            int hours = TimeZone.CurrentTimeZone.GetUtcOffset(datetime).Hours;
            int minutes = TimeZone.CurrentTimeZone.GetUtcOffset(datetime).Minutes;
            if (hours == 0 && minutes == 0)
                return sb.ToString();
            else
            {
                string offset = (hours >= 0 && minutes >= 0 ? "+" : "") + hours.ToString("00") + minutes.ToString("00");
                return sb.ToString().Replace("GMT", offset);
            }
        }

        internal static ImageCodecInfo GetCodec(string codec)
        {
            foreach (ImageCodecInfo ice in ImageCodecInfo.GetImageEncoders())
            {
                if (ice.MimeType == codec)
                    return ice;
            }
            return null;
        }

        /// <summary>
        /// For developers only
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Advanced)]
        public static void SaveJpeg(System.Drawing.Image image, Stream buff, int quality)
        {
            ImageCodecInfo ici = ExportUtils.GetCodec("image/jpeg");
            EncoderParameters ep = new EncoderParameters();
            ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
            image.Save(buff, ici, ep);
        }

        internal static string TruncLeadSlash(string line)
        {
            line = line.Replace("\\", "/");
            if (line.StartsWith("/"))
                return line.Remove(0, 1);
            else
                return line;
        }

        internal static string GetCellReference(int x, int y)
        {
            StringBuilder cellReference = new StringBuilder(4);
            const int MAX_CHARS = 'Z' - 'A' + 1;

            do
            {
                if (cellReference.Length > 0)
                    x--;
                int digit = x % MAX_CHARS;
                char cellDig = (char)((char)'A' + (char)digit);
                cellReference.Append(cellDig);
                x -= digit;
                x /= (MAX_CHARS);
            }
            while (x > 0);

            for (int i = 0; i < cellReference.Length / 2; i++)
            {
                char c = cellReference[i];
                cellReference[i] = cellReference[cellReference.Length - i - 1];
                cellReference[cellReference.Length - i - 1] = c;
            }

            cellReference.Append(y.ToString());
            return cellReference.ToString();
        }

        private static readonly CultureInfo INVARIANT_CULTURE = CultureInfo.InvariantCulture;
        internal static string StringFormat(string formatString, params object[] objects)
        {
            return String.Format(INVARIANT_CULTURE, formatString, objects);
        }

        /// <summary>
        /// Convert index to Excel column name.
        /// </summary>
        /// <param name="index"> Index of column</param>
        /// <returns> Column name</returns>
        public static string IndexToName(int index)
        {

            bool firstSymbol = false;
            string result = "";

            for (; index > 0; index /= 26)
            {
                if (index < 26 && result != "")
                    firstSymbol = true;

                var x = index % 26;
                result = (char)(x + 'A' + (firstSymbol ? -1 : 0)) + result;
            }

            if (result == "")
                result = "A";

            return result;
        }

        static ExportUtils()
        {
            var defaultProvider = new NumberFormatInfo();
            defaultProvider.NumberGroupSeparator = string.Empty;
            defaultProvider.NumberDecimalSeparator = ".";
            _provider = NumberFormatInfo.ReadOnly(defaultProvider);
        }
    }
}

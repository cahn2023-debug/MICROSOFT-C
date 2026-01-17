using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace OfflineProjectManager.Utils
{
    public static class VietnameseTextHelper
    {
        private const string NOACCENT_MARKER = "\n__NOACCENT__\n";
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<char, (char baseChar, bool hasAccent)> _charCache = 
            new System.Collections.Concurrent.ConcurrentDictionary<char, (char, bool)>();

        /// <summary>
        /// Chuẩn hóa Unicode NFC (Dựng sẵn) và chuyển về chữ thường.
        /// </summary>
        public static string NormalizeText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Normalize(NormalizationForm.FormC).ToLower();
        }

        /// <summary>
        /// Loại bỏ dấu tiếng Việt để tìm kiếm không dấu.
        /// Chuyển đ -> d.
        /// </summary>
        public static string RemoveAccents(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            // Normalize NFD để tách dấu ra khỏi ký tự gốc
            string normalizedString = text.Normalize(NormalizationForm.FormD);
            StringBuilder stringBuilder = new StringBuilder();

            foreach (char c in normalizedString)
            {
                UnicodeCategory unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            // Chuyển về NFC và xử lý đ -> d
            string result = stringBuilder.ToString().Normalize(NormalizationForm.FormC).ToLower();
            
            // Xử lý đ, Đ riêng biệt vì NFD không tách đ thành d + dấu
            result = result.Replace('đ', 'd');
            
            return result;
        }

        /// <summary>
        /// Tạo chuỗi không dấu và mapping index để highlight chính xác.
        /// </summary>
        public static (string noAccentText, List<int> indexMap) BuildNoAccentAndMap(string textNfc)
        {
            if (string.IsNullOrEmpty(textNfc)) return ("", new List<int>());

            StringBuilder noAccentBuilder = new StringBuilder(textNfc.Length);
            List<int> idxMap = new List<int>(textNfc.Length);

            for (int i = 0; i < textNfc.Length; i++)
            {
                char ch = textNfc[i];
                
                if (!_charCache.TryGetValue(ch, out var cached))
                {
                    string decomposed = ch.ToString().Normalize(NormalizationForm.FormD);
                    char baseChar = ' ';
                    bool foundBase = false;

                    foreach (char dc in decomposed)
                    {
                        if (CharUnicodeInfo.GetUnicodeCategory(dc) != UnicodeCategory.NonSpacingMark)
                        {
                            baseChar = char.ToLowerInvariant(dc);
                            if (baseChar == 'đ') baseChar = 'd';
                            foundBase = true;
                            break;
                        }
                    }

                    if (!foundBase) baseChar = char.ToLowerInvariant(ch);
                    cached = (baseChar, decomposed.Length > 1 || baseChar != char.ToLowerInvariant(ch));
                    _charCache.TryAdd(ch, cached);
                }

                noAccentBuilder.Append(cached.baseChar);
                idxMap.Add(i);
            }

            return (noAccentBuilder.ToString(), idxMap);
        }
    }
}

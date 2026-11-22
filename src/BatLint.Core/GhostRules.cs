using System;

namespace BatLint.Core
{
    /// <summary> 行内灰字预测规则：根据光标前的行文本推断后续内容 / Ghost text rules derived from the text before the caret. </summary>
    internal static class GhostRules
    {
        /// <summary> 固定前缀 → 预测文本（按前缀长度取最长匹配）/ Fixed prefix to ghost text; longest prefix wins. </summary>
        private static readonly string[,] Fixed =
        {
            { "setlocal ", "enabledelayedexpansion" },
            { "set /a ", "A=1+2" },
            { "set /p ", "A=请输入:" },
            { "set ", "\"A=1\"" },
            { "if not exist ", "\"path\" (" },
            { "if exist ", "\"path\" (" },
            { "if errorlevel ", "1 (" },
            { "if defined ", "A (" },
            { "if ", "\"%1\"==\"\" (" },
            { "for /f ", "\"delims=\" %%i in (f.txt) do" },
            { "for /r ", "%%i in (*.bat) do" },
            { "for /l ", "%%i in (1,1,10) do" },
            { "for /d ", "%%i in (*) do" },
            { "for ", "%%i in (*.*) do" },
            { "cd /d ", "\"D:\\\"" },
            { "cd ", "/d \"D:\\\"" },
            { "pushd ", "\"%~dp0\"" },
            { "call ", ":label" },
            { "goto ", ":eof" },
            { "start ", "\"\" \"app.exe\"" },
            { "xcopy ", "\"src\" \"dst\" /e /i /y" },
            { "robocopy ", "\"src\" \"dst\" /e /r:1 /w:1" },
            { "del ", "\"*.tmp\" /f /q" },
            { "rd ", "/s /q \"dir\"" },
            { "md ", "\"dir\"" },
            { "type ", "\"f.txt\"" },
            { "title ", "\"窗口标题\"" },
            { "timeout ", "/t 5 /nobreak" },
            { "taskkill ", "/f /im \"name.exe\"" },
            { "chcp ", "65001 >nul" },
            { "net use ", "\"\\\\server\\share\"" },
            { "net ", "use \"\\\\server\\share\"" },
            { "reg add ", "\"HKCU\\Software\\App\" /v Name /t REG_SZ /d Value /f" },
            { "reg query ", "\"HKLM\\Software\\App\" /v Name" },
            { "reg ", "add \"HKCU\\Software\\App\" /v Name /d Value /f" },
            { "sc ", "query \"service\"" },
            { "schtasks ", "/query /fo LIST" },
            { "echo ", "hello" }
        };

        /// <summary> 推断光标后应出现的文本，无匹配返回 null / Predict the text after the caret, null when unknown. </summary>
        public static string Predict(string before)
        {
            if (string.IsNullOrEmpty(before)) return null;

            var t = before.TrimStart();
            if (t.Length == 0) return null;
            if (t.StartsWith("REM", StringComparison.OrdinalIgnoreCase) || t.StartsWith("::")) return null;

            var setGhost = PredictSet(t);
            if (setGhost != null) return setGhost;

            string best = null;
            var bestLen = 0;

            for (var i = 0; i < Fixed.GetLength(0); i++)
            {
                var prefix = Fixed[i, 0];
                if (prefix.Length <= bestLen) continue;
                if (!t.EndsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

                best = Fixed[i, 1];
                bestLen = prefix.Length;
            }

            return best;
        }

        /// <summary> set " 之后的智能补全：变量名、等号、值的逐段推断 / Stepwise prediction inside an open set quote. </summary>
        private static string PredictSet(string t)
        {
            if (!t.StartsWith("set ", StringComparison.OrdinalIgnoreCase)) return null;

            var q = t.IndexOf('"');
            if (q < 0) return null;
            if (t.IndexOf('"', q + 1) >= 0) return null;          // 引号已闭合

            var inner = t.Substring(q + 1);
            if (inner.Length == 0) return "A=1\"";                // set "        → A=1"
            if (inner.IndexOf('=') < 0) return "=1\"";            // set "A       → =1"

            return inner.EndsWith("=") ? "1\"" : "\"";            // set "A= → 1"   set "A=1 → "
        }
    }
}

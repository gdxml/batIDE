using System;
using System.Text.RegularExpressions;

namespace BatLint.Core
{
    /// <summary>
    /// 固定语法形态校验。诊断只说「期望什么、实际找到什么」，
    /// 不做模糊匹配、不报相似度（相似度只属于建议层，见 SpellSuggest）。
    /// </summary>
    internal static class Grammar
    {
        private static readonly Regex IfKeywordForm = new Regex(
            @"^if\s+(?:/i\s+)?(?:not\s+)?(errorlevel|exist|defined|cmdextversion)\s+\S",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex IfOperatorForm = new Regex(
            @"^if\s+(?:/i\s+)?(?:not\s+)?.+?\s(equ|neq|lss|leq|gtr|geq)\s\S",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ForValid = new Regex(
            @"^for\s+(?:/[a-zA-Z]\s+)*(?:/f\s+(?:""[^""]*""\s+)?)?%%?[A-Za-z]\s+in\s+\(.*\)\s+do(?:\s|$)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ForMissingDo = new Regex(
            @"^for\s+(?:/[a-zA-Z]\s+)*(?:/f\s+(?:""[^""]*""\s+)?)?%%?[A-Za-z]\s+in\s+\(.*\)\s+\S",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ForMissingParen = new Regex(
            @"^for\s+(?:/[a-zA-Z]\s+)*(?:/f\s+)?%%?[A-Za-z]\s+in\s+(?!\()",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary> if 期望的写法，供消息里列出来 / Expected shapes listed in the message. </summary>
        private const string IfShapes =
            "if [not] A==B、if A equ B（neq/lss/leq/gtr/geq）、if exist 路径、if defined 变量、if errorlevel N";

        private const string ForShapes = "for [开关] %%v in (集合) do 命令";

        private const string SetShapes = "set VAR=值、set \"VAR=值\"、set /a 表达式、set /p VAR=";

        /// <summary> 一条形态校验结论 / Result of a shape check. </summary>
        internal sealed class Verdict
        {
            public Verdict(string rule, string message)
            {
                Rule = rule;
                Message = message;
            }

            public string Rule { get; }
            public string Message { get; }
        }

        /// <summary> if 语句形态校验 / Validate the shape of an if statement. </summary>
        public static Verdict CheckIf(string line)
        {
            var t = line.TrimStart();
            if (!t.StartsWith("if", StringComparison.OrdinalIgnoreCase)) return null;
            if (t.Length > 2 && !char.IsWhiteSpace(t[2])) return null;

            if (IfKeywordForm.IsMatch(t)) return null;
            if (IfOperatorForm.IsMatch(t)) return null;

            // 赋值符误用：+= -= *= /=
            var assignment = Regex.Match(t, @"([-+*/]=(?!=))");
            if (assignment.Success)
                return new Verdict("R47",
                    $"if 里出现了赋值符 {assignment.Groups[1].Value}，这里期望比较符 ==；cmd 会报「命令语法不正确」");

            var eq = t.IndexOf("==", StringComparison.Ordinal);
            if (eq > 0)
            {
                var left = t.Substring(0, eq).Trim();
                var right = t.Substring(eq + 2).Trim();

                if (right.IndexOf("==", StringComparison.Ordinal) >= 0)
                    return new Verdict("R51", "if 比较里出现了多个 ==，只有第一个生效；期望只保留一个比较符");

                if (left.Length == 0)
                    return new Verdict("R48", "if 比较缺少左侧操作数；期望 if A==B");

                if (right.Length == 0) return null;      // cmd 允许，交给其它规则

                return null;
            }

            // 单个 =
            var single = Regex.Match(t, @"(?<![=<>!])=(?!=)");
            if (single.Success)
                return new Verdict("R47", "if 比较用了单个 =，这里期望 ==；cmd 会报「命令语法不正确」");

            var found = Head(t);
            if (found.Length == 0)
                return new Verdict("R48", $"if 后面缺少条件；期望 {IfShapes}");

            return new Verdict("R46", $"无法识别的 if 写法 `{found}`；期望 {IfShapes}");
        }

        /// <summary> for 语句形态校验 / Validate the shape of a for statement. </summary>
        public static Verdict CheckFor(string line)
        {
            var t = line.TrimStart();
            if (!t.StartsWith("for", StringComparison.OrdinalIgnoreCase)) return null;
            if (t.Length > 3 && !char.IsWhiteSpace(t[3])) return null;

            if (ForValid.IsMatch(t)) return null;

            if (ForMissingParen.IsMatch(t))
                return new Verdict("R49", $"for 的集合缺少括号；期望 {ForShapes}");

            if (ForMissingDo.IsMatch(t))
                return new Verdict("R49", "for 缺少 do 关键字；期望 for %%i in (...) do 命令");

            return new Verdict("R49", $"无法识别的 for 写法 `{Head(t)}`；期望 {ForShapes}");
        }

        /// <summary> set 语句形态校验 / Validate the shape of a set statement. </summary>
        public static Verdict CheckSet(string line)
        {
            var t = line.TrimStart();
            if (!t.StartsWith("set", StringComparison.OrdinalIgnoreCase)) return null;
            if (t.Length > 3 && !char.IsWhiteSpace(t[3])) return null;

            var body = t.Substring(3).Trim();

            if (body.Length == 0) return null;                                            // set：列出全部
            if (body.StartsWith("/a", StringComparison.OrdinalIgnoreCase)) return null;    // set /a 表达式
            if (body.StartsWith("/p", StringComparison.OrdinalIgnoreCase)) return null;    // set /p VAR=
            if (body.Contains("=")) return null;                                          // 赋值（等号空格由 R1 管）
            if (Regex.IsMatch(body, @"^""?[A-Za-z_]\w*""?$")) return null;                 // set A：显示单个变量

            return new Verdict("R50", $"set 缺少 =；期望 {SetShapes}");
        }

        /// <summary> 取 if/for 头部的实际写法，用于消息里的反引号 / The part after the keyword. </summary>
        private static string Head(string t)
        {
            var s = t.Trim();
            if (s.StartsWith("if", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2).TrimStart();
            else if (s.StartsWith("for", StringComparison.OrdinalIgnoreCase)) s = s.Substring(3).TrimStart();

            if (s.StartsWith("/i", StringComparison.OrdinalIgnoreCase)) s = s.Substring(2).TrimStart();
            if (s.StartsWith("not", StringComparison.OrdinalIgnoreCase)) s = s.Substring(3).TrimStart();
            return s.Length > 40 ? s.Substring(0, 40) + "…" : s;
        }
    }
}

﻿using Microsoft.CodeAnalysis;

public static class Utility
{
    /// <summary> 生成コードの最初に付けるやつ </summary>
    public const string Header = @"// <auto-generated/>
#nullable enable
#pragma warning disable CS8600
#pragma warning disable CS8601
#pragma warning disable CS8602
#pragma warning disable CS8603
#pragma warning disable CS8604
#pragma warning disable CS8618
";

    /// <summary>
    /// パスカルケースに変換する
    /// </summary>
    public static string ToPascalCase(this string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            return string.Empty;
        }

        var tmp = str.AsSpan();

        // 1文字目がアンダースコアなら削除
        if (tmp[0] == '_')
        {
            tmp = tmp.Slice(1);
        }

        // 1文字目を大文字に変換し、残りを結合
        return char.ToUpper(tmp[0]) + tmp.Slice(1).ToString();
    }

    /// <summary>
    /// キャメルケースに変換する
    /// </summary>
    public static string ToCamelCase(this string str, string prefix = "")
    {
        if (string.IsNullOrEmpty(str))
        {
            return string.Empty;
        }

        var tmp = str.AsSpan();

        // 1文字目がアンダースコアなら削除
        if (tmp[0] == '_')
        {
            tmp = tmp.Slice(1);
        }

        // 1文字目を小文字に変換し、残りを結合
        return prefix + char.ToLower(tmp[0]) + tmp.Slice(1).ToString();
    }

    /// <summary>
    /// 指定した型を継承しているか返す
    /// </summary>
    public static bool InheritsFrom(this INamedTypeSymbol derived, INamedTypeSymbol baseType)
    {
        var current = derived;
        while (current.BaseType != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current.BaseType, baseType))
            {
                return true;
            }
            current = current.BaseType;
        }
        return false;
    }
}
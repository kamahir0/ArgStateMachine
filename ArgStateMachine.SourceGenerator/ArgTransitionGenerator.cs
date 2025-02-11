using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.DotnetRuntime.Extensions;
using System.Text;

/// <summary>
/// 特定ステートへの引数付き遷移メソッドをステートマシンに生やすソースジェネレータ
/// </summary>
[Generator(LanguageNames.CSharp)]
public class ArgTransitionGenerator : IIncrementalGenerator
{
    // 生成トリガーとなるアトリビュートのFullQualifiedMetadataName（完全修飾名）
    private const string EnableArgTransitionAttribute = "ArgStateMachine.EnableArgTransitionAttribute";
    private const string MarkAsOnEnterAttribute = "ArgStateMachine.MarkAsOnEnterAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // "EnableArgTransition"アトリビュートを持つステートマシンクラスを探す
        var stateMachineClassProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            context,
            EnableArgTransitionAttribute,
            static (node, token) => true,
            static (context, token) => context)
            .Collect();

        // "MarkAsOnEnter"アトリビュートを持つステートのメソッドを探す
        var onEnterMethodProvider = context.SyntaxProvider.ForAttributeWithMetadataName(
            context,
            MarkAsOnEnterAttribute,
            static (node, token) => true,
            static (context, token) => context)
            .Collect();

        // 結合する
        var combined = stateMachineClassProvider.Combine(onEnterMethodProvider);

        // ソースコード生成処理
        context.RegisterSourceOutput(combined, static (context, source) =>
        {
            var stateMachineClassAttributes = source.Left;
            var onEnterMethodAttributes = source.Right;

            foreach(var stateMachineClassAttribute in stateMachineClassAttributes)
            {
                var stateMachineClassSymbol = (INamedTypeSymbol)stateMachineClassAttribute.TargetSymbol;

                // StateMachine<TState, TContext>を継承しているか確かめる
                var baseType = stateMachineClassSymbol.BaseType;
                if (baseType == null || baseType.TypeArguments.Length != 2 || baseType.TypeArguments[0] == null || baseType.TypeArguments[1] == null)
                {
                    return;
                }
                // TState（ステートの基底型）
                var stateBaseClassSymbol = (INamedTypeSymbol)(baseType.TypeArguments[0]);

                // stateMethodSource から、そのメソッドが TState の派生クラスに属しているものをフィルタ
                var stateMethodSymbols = onEnterMethodAttributes
                    .Select(m => (MethodSymbol: (IMethodSymbol)(m.TargetSymbol), StateClassSymbol: ((IMethodSymbol)m.TargetSymbol).ContainingType))
                    .Where(entry => entry.StateClassSymbol.InheritsFrom(stateBaseClassSymbol)) // TState の派生クラスかチェック
                    .Select(entry => entry.MethodSymbol)
                    .ToArray();

                // ステートマシンのpartialコード生成
                EmitPartialStateMachine(context, stateMachineClassSymbol, stateMethodSymbols);

                // ステート基底クラスのpartialコード生成
                EmitPartialStateBase(context, stateMachineClassSymbol, stateBaseClassSymbol);
            }
        });
    }

    /// <summary>
    /// ステートマシンのpartialコード生成処理
    /// </summary>
    private static void EmitPartialStateMachine(SourceProductionContext context, INamedTypeSymbol stateMachineClassSymbol, IMethodSymbol[] stateMethodSymbols)
    {
        // クラスのネームスペースを取得
        var namespaceSymbol = stateMachineClassSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : stateMachineClassSymbol.ContainingNamespace;
        var namespaceStr = namespaceSymbol == null ? string.Empty : $"namespace {namespaceSymbol}";

        // 生成するコードのビルダー
        var constructorSb = new StringBuilder();
        var switchSb = new StringBuilder();
        var perStateSb = new StringBuilder();

        // 引数を横流しするだけのコンストラクタを自動定義
        constructorSb.AppendLine("        /// <summary> コンストラクタ </summary>");
        constructorSb.AppendLine($"        public {stateMachineClassSymbol.Name}({stateMachineClassSymbol.BaseType.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)} context, global::System.Collections.Generic.IReadOnlyList<{stateMachineClassSymbol.BaseType.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}> states) : base(context, states) {{ }}");

        // 全てのステートに共通する接頭辞を見つける
        var commonPrefix = GetCommonPrefix(stateMethodSymbols);

        // ステートごとのコードを作る
        foreach ( var stateMethodSymbol in stateMethodSymbols )
        {
            var result = EmitPerState(stateMethodSymbol, commonPrefix);
            switchSb.AppendLine(result.SwitchStr);
            perStateSb.AppendLine(result.PerStateStr);
        }

        // ステートマシンクラスの完全修飾名をファイル名とする
        var fileName = stateMachineClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "")
            .Replace("<", "_")
            .Replace(">", "_");

        // ステートマシンのソースコードを追加
        context.AddSource($"{fileName}.g.cs",
$@"{Utility.Header}
{namespaceStr}
{{
    partial class {stateMachineClassSymbol.Name}
    {{
{constructorSb}
        protected override bool PrioritizeCustomOnEnter()
        {{
            switch (CurrentState)
            {{
{switchSb}
                default:
                    return false;
            }}
        }}
{perStateSb}
    }}
}}"
        );
    }

    // ステートごとのコードを作る
    private static (string SwitchStr, string PerStateStr) EmitPerState(IMethodSymbol stateMethodSymbol, string commonPrefix)
    {
        // ステートクラスの完全修飾名
        var fullyQualifiedStateName = stateMethodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // ステート名を取得
        var stateName = stateMethodSymbol.ContainingType.Name;

        // 短縮ステート名を取得
        var shortStateName = stateName;
        if (!string.IsNullOrWhiteSpace(commonPrefix))
        {
            // 全ステートに共通の接頭辞があった場合は削って短縮する
            shortStateName = shortStateName.Substring(commonPrefix.Length);
        }

        // メソッドの引数を取得
        (string Type, string Name)[] parameters = stateMethodSymbol.Parameters
        .Select(param => (param.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), param.Name))
        .ToArray();
        var fullParametersString = string.Join(", ", parameters.Select(p => $"{p.Item1} {p.Item2}"));

        // ステートごとにメソッドを生やすコードを作る
        var perStateSb = new StringBuilder();

        // 引数保持クラス
        var argumentsClassName = $"{stateMethodSymbol.ContainingType.Name}Arguments";
        var argumentsClassFieldName = argumentsClassName.ToCamelCase("_");
        var argumentsClassProperySb = new StringBuilder();
        var argumentsClassConstructorSb = new StringBuilder();
        foreach (var param in parameters)
        {
            argumentsClassProperySb.AppendLine($"            public {param.Type} {param.Name.ToPascalCase()};");
            argumentsClassConstructorSb.AppendLine($"                {param.Name.ToPascalCase()} = {param.Name};");
        }
        perStateSb.AppendLine($@"
        // 引数保持用フィールド
        private {argumentsClassName} {argumentsClassFieldName} = new {argumentsClassName}();

        private class {argumentsClassName}
        {{
{argumentsClassProperySb}
            public void Set({fullParametersString})
            {{
{argumentsClassConstructorSb}
            }}
        }}
");
        // 遷移メソッド
        var scheduleTransitionMethodName = $"ScheduleTransitionTo{shortStateName}";
        var transitionMethodName = $"TransitionTo{shortStateName}";
        var joinedParametersString = string.Join(", ", parameters.Select(p => p.Name));
        perStateSb.AppendLine($@"
        /// <summary> {stateName}への遷移をスケジュールする </summary>
        public void {scheduleTransitionMethodName}({fullParametersString})
        {{
            TransitionQueue.Enqueue(typeof({stateMethodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}));
            {argumentsClassFieldName}.Set({joinedParametersString});
        }}

        /// <summary> {stateName}への遷移を実行する </summary>
        public void {transitionMethodName}({fullParametersString})
        {{
            {scheduleTransitionMethodName}({joinedParametersString});
            ExecuteTransitionQueue();
        }}
");
        // swich文の中身を作る
        var switchSb = new StringBuilder();
        var passArgumentsString = string.Join(", ", parameters.Select(p => $@"{argumentsClassFieldName}.{p.Name.ToPascalCase()}"));
        switchSb.AppendLine($@"                case var state when state is {fullyQualifiedStateName} s:");
        switchSb.AppendLine($@"                    s.{stateMethodSymbol.Name}({passArgumentsString});");
        switchSb.AppendLine($@"                    return true;");

        return (switchSb.ToString(), perStateSb.ToString());
    }

    /// <summary>
    /// ステート基底クラスのpartialコード生成処理
    /// </summary>
    private static void EmitPartialStateBase(SourceProductionContext context, INamedTypeSymbol stateMachineClassSymbol, INamedTypeSymbol stateBaseClassSymbol)
    {
        // クラスのネームスペースを取得
        var namespaceSymbol = stateBaseClassSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : stateBaseClassSymbol.ContainingNamespace;
        var namespaceStr = namespaceSymbol == null ? string.Empty : $"namespace {namespaceSymbol}";

        // ステートマシンクラスの完全修飾名を取得
        var fullyQualifiedStateMachineName = stateMachineClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        // ステート基底クラスの完全修飾名をファイル名とする
        var fileName = stateBaseClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("global::", "")
            .Replace("<", "_")
            .Replace(">", "_");

        // ステートのベースクラスのソースコードを追加
        context.AddSource($"{fileName}.g.cs",
$@"{Utility.Header}
{namespaceStr}
{{
    partial class {stateBaseClassSymbol.Name}
    {{
        /// <summary> ステートマシン </summary>
        protected new {fullyQualifiedStateMachineName} StateMachine => ({fullyQualifiedStateMachineName})base.StateMachine;
    }}
}}
"
        );
    }

    // 共通接頭辞を導く
    private static string GetCommonPrefix(IMethodSymbol[] methodSymbols)
    {
        // 長さが1なら空文字を返す（「共通接頭辞は無かった」とする）
        if (methodSymbols.Length < 2) return string.Empty;

        var methodNames = methodSymbols.Select(m => m.ContainingType.Name).ToArray();

        if (methodNames.Length == 0) return string.Empty;
        if (methodNames.Length == 1) return methodNames[0];

        string prefix = methodNames[0];

        for (int i = 1; i < methodNames.Length; i++)
        {
            int j = 0;
            while (j < prefix.Length && j < methodNames[i].Length && prefix[j] == methodNames[i][j])
            {
                j++;
            }

            prefix = prefix.Substring(0, j);

            if (string.IsNullOrEmpty(prefix))
            {
                break;
            }
        }

        return prefix;
    }

}
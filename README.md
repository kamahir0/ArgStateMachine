# ArgStateMachine
A state machine that can transition with arguments for Unity.

[![license](https://img.shields.io/badge/LICENSE-MIT-green.svg)](LICENSE)
![unity-version](https://img.shields.io/badge/unity-2022.3+-000.svg)
[![releases](https://img.shields.io/github/release/kamahir0/ArgStateMachine.svg)](https://github.com/kamahir0/ArgStateMachine/releases)

ArgStateMachineはUnity用に制作したステートマシンです。Source Generatorによって、特定のステート遷移時に引数を渡すことが可能になるというユニークな機能を備えています。

## 概要
このライブラリが提供するのはステートマシンです。一応、ごく普通のステートマシンとしても使うことはできます。

```
StateMachine<BattleContext, BattleStateBase> _stateMachine;

// キャラ選択ステートに遷移
_stateMachine.Transition<BattleCharacterSelectionState>();

/// <summary>
/// 行動するキャラを選択するステート
/// </summary>
public class BattleCharacterSelectionState : BattleStateBase
{
    // ステート遷移時処理
    protected override void OnEnter()
    {
        // ....
    }
}
```

ところで、ステートマシンを使っていると「**遷移時にデータを受け取りたい**」と思うことがあります。

しかしステートマシンの性質上、遷移時に呼ばれる、いわゆる`OnEnter`メソッドは大抵引数無しで固定であり、
必然的にコンテキストを介してデータを受け渡すことになります。

コンテキストを介して受け渡していくと、やがてコンテキストのフィールドは

- 「どのタイミングで更新されるのか」
- 「このタイミングで参照して、データが古いままになっていないだろうか」

などの把握が難しくなっていきます。

`// 次に○○ステートに遷移するのを見越して、コンテキストに××を代入しておく`

みたいなコメントを残し始めるともう滅茶苦茶です。

このライブラリ「`ArgStateMachine`」が提供するステートマシンは、ソースジェネレータの力で「**引数付き遷移**」を実現します。

```
// 対象選択ステートに遷移
_stateMachine.TransitionToTargetSelectionState(actor, skill);

/// <summary>
/// スキルの対象を選択するステート
/// </summary>
public class BattleTargetSelectionState : BattleStateBase
{
    private Actor _selectedPlayer;
    private Skill _selectedSkill;

    [MarkAsOnEnter]
    public void OnEnter(Actor selectedPlayer, Skill selectedSkill)
    {
        _selectedPlayer = selectedPlayer;
        _selectedSkill = selectedSkill;
    }

    protected void OnUpdate()
    {
        // ....
    }
}
```

↑の例にある**TransitionToTargetSelectionState**というメソッドは、ソースジェネレータによって生成されるメソッドです。

対象選択ステートクラス側で定義する`OnEnter(Actor selectedPlayer, Skill selectedSkill)`というメソッドに対応して、同じ引数をとる専用遷移メソッドがステートマシンに生えてきた訳です。

局所的な箇所でしか使わない情報についてはコンテキストを使わず引数で渡すようにすれば、コンテキストの見通しはだいぶ良くなるはずです。

- ソースジェネレータで生成されたメソッドはIDE上でもバッチリ補完されます
    - ![image](https://github.com/user-attachments/assets/e5a13893-40cf-45c8-a00a-fdc8515be490)

## セットアップ
Unity 2022.3 以上で動作確認済み

### インストール
1. Window > Package ManagerからPackage Managerを開く
2. 「+」ボタン > Add package from git URL
3. 以下のURLを入力する

```
https://github.com/kamahir0/ArgStateMachine.git?path=ArgStateMachine/Assets/ArgStateMachine
```

## 使い方
ざっくり説明します

まずは普通のステートマシンとして使う場合

### StateBase
抽象クラスです

ステートマシンで取り扱うステートクラスは、必ずこれを継承する必要があります。
`OnEnter`, `OnUpdate`, `OnExit`といったメソッドを任意でオーバーライドすることで、ステートの振る舞いを作ることができます

### StateMachine<TContext, TState>
ステートマシン本体です

| メソッド名                  | 説明                                                   |  
|-----------------------------|------------------------------------------------------|  
| **Transition＜T＞()**         | `T` ステートに即座に遷移する                          |  
| **ScheduleTransition＜T＞()** | `T` ステートへの遷移をスケジュールする（キューに積む） |  
| **ExecuteTransitionQueue()** | キューに積まれた遷移を全て順次実行する                 |  
| **Undo()**                  | 一つ前のステートに即座に遷移する                       |  
| **ScheduleUndo()**          | 一つ前のステートへの遷移をスケジュールする（キューに積む） |  
| **Update()**                | 現在のステートの `OnUpdate()` を呼び出す               |  

コンテキストとなる`TContext`には何の制約もないので、好きなクラスを設定できます。
引数付き遷移機能を使わないままならこのままで利用できますが、使うなら継承した自作クラスが必要になります

### 引数付き遷移
以下の対応が必要です
- StateMachineを継承した自作クラス(A)の作成
    - partialにする
- StateBaseを継承した、自作クラス(B)の作成
    - 共通のベースクラスとして使う
    - partialにする
    - ![image](https://github.com/user-attachments/assets/a38ebe05-24bf-41d5-afdd-b931b20f7180)
- (A)に`[EnableArgTransition]`属性を付ける
    - ![image](https://github.com/user-attachments/assets/7acc6d23-3e86-4376-8cc5-1ff6f20eb7d0)
- (B)を継承したクラスに、引数付き遷移時に呼び出したいメソッドを定義し、`[MarkAsOnEnter]`属性を付ける
    - ![image](https://github.com/user-attachments/assets/fcc00cc9-bd18-4bb0-87d8-6a3140edd066)

## 使用例
このライブラリの`Sandbox`フォルダ配下に、実際にステートマシンで作成したミニマルなコマンドバトルRPGがあります

Unityプロジェクトにインストールしていれば、`SampleGame.asset`というScriptableObjectのインスペクタで実際に遊べるようになっています

- キャラ選択ステート
    - ![image](https://github.com/user-attachments/assets/218d3cea-5797-4292-989e-31b6ab934504)

- スキル選択ステート
    - ![image](https://github.com/user-attachments/assets/887600db-bec8-472b-9621-7d935784c33e)

## 生成コード
詳細は省きますが、こんな感じのコードが生成されます

![image](https://github.com/user-attachments/assets/fe65581c-e19e-4d87-be57-6d614f82fc79)

![image](https://github.com/user-attachments/assets/5529fe06-f931-4b93-b8c4-3ad9ed067da9)

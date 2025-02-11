using System.Collections.Generic;

namespace ArgStateMachine.Sandbox
{
    /// <summary>
    /// バトルの進行状態を制御するステートマシン
    /// </summary>
    [EnableArgTransition] // 引数付き遷移を有効化するためにこれを付ける
    public partial class BattleStateMachine : StateMachine<BattleStateBase, BattleContext> { }

    /// <summary>
    /// コンテキスト
    /// </summary>
    public class BattleContext
    {
        public List<Actor> Players;
        public List<Actor> Monsters;
    }

    /// <summary>
    /// バトルに登場するキャラクター
    /// </summary>
    public class Actor
    {
        public string Name;
        public int Hp;
        public List<Skill> Skills;
    }

    /// <summary>
    /// バトルで使うスキル
    /// </summary>
    public class Skill
    {
        public string Name;
        public int Power;
        public bool TargetIsMonster;
    }
}

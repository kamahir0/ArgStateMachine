using UnityEngine;
using UnityEditor;

namespace ArgStateMachine.Sandbox
{
    /// <summary>
    /// バトルステートの基底クラス
    /// </summary>
    public abstract partial class BattleStateBase : StateBase<BattleStateBase, BattleContext>
    {
        public abstract void OnInspectorGUI();

        // ヘッダ描画
        protected void DrawHeader()
        {
            EditorGUILayout.LabelField($"{base.StateMachine.PreviousState?.GetType().Name}", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(" ↓", EditorStyles.miniLabel);
            EditorGUILayout.LabelField($"{base.StateMachine.CurrentState.GetType().Name}");
            EditorGUILayout.Space();
        }

        // アクターのステータスを描画
        protected void DrawActorStatus(Actor actor)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(actor.Name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"HP:{actor.Hp}");
            EditorGUILayout.EndVertical();
        }

        // スキル情報を描画
        protected void DrawSkillInfo(Skill skill)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(skill.Name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Power:{skill.Power}");
            EditorGUILayout.EndVertical();
        }

        // 戻るボタンを描画
        protected void DrawBackButton()
        {
            EditorGUILayout.Space();
            if (GUILayout.Button("戻る"))
            {
                StateMachine.Undo();
            }
        }
    }

    /// <summary>
    /// 行動するキャラを選択するステート
    /// </summary>
    public class BattleCharacterSelectionState : BattleStateBase
    {
        public override void OnInspectorGUI()
        {
            DrawHeader();

            // 生存しているキャラのステータスを表示
            Context.Players.ForEach(actor => DrawActorStatus(actor));
            Context.Monsters.ForEach(actor => DrawActorStatus(actor));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("行動するキャラクターを選べ", EditorStyles.boldLabel);

            // キャラ選択ボタンを表示
            foreach (var player in Context.Players)
            {
                if (GUILayout.Button(player.Name))
                {
                    StateMachine.TransitionToSkillSelectionState(player);
                }
            }
        }
    }

    /// <summary>
    /// 使用スキルを選択するステート
    /// </summary>
    public class BattleSkillSelectionState : BattleStateBase
    {
        private Actor _selectedPlayer;

        [MarkAsOnEnter]
        public void OnEnter(Actor selectedPlayer)
        {
            _selectedPlayer = selectedPlayer;
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();

            // 選択中キャラのステータスを表示
            DrawActorStatus(_selectedPlayer);

            // 選択中キャラの持つスキル一覧を表示
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("スキル一覧", EditorStyles.boldLabel);
            foreach (var skill in _selectedPlayer.Skills)
            {
                DrawSkillInfo(skill);
            }

            // スキル選択ボタンを表示
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("スキルを選べ", EditorStyles.boldLabel);
            foreach (var skill in _selectedPlayer.Skills)
            {
                if (GUILayout.Button(skill.Name))
                {
                    StateMachine.TransitionToTargetSelectionState(_selectedPlayer, skill);
                }
            }

            // キャラ選択ステートに戻るボタンを表示
            DrawBackButton();
        }
    }

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

        public override void OnInspectorGUI()
        {
            DrawHeader();

            // 選択中キャラ・スキルを表示
            DrawActorStatus(_selectedPlayer);
            DrawSkillInfo(_selectedSkill);

            // スキルに応じて対象リストを変える
            var targets = _selectedSkill.TargetIsMonster ? Context.Monsters : Context.Players;

            // 対象のステータスを表示
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("対象一覧", EditorStyles.boldLabel);
            foreach (var target in targets)
            {
                DrawActorStatus(target);
            }

            // 対象選択ボタンを表示
            EditorGUILayout.LabelField("対象を選べ", EditorStyles.boldLabel);
            foreach (var target in targets)
            {
                if (GUILayout.Button(target.Name))
                {
                    StateMachine.TransitionToPlayerTurnState(_selectedPlayer, _selectedSkill, target);
                }
            }

            // スキル選択ステートに戻るボタンを表示
            DrawBackButton();
        }
    }

    /// <summary>
    /// プレイヤーターンを処理するステート
    /// </summary>
    public class BattlePlayerTurnState : BattleStateBase
    {
        [MarkAsOnEnter]
        public void OnEnter(Actor selectedPlayer, Skill selectedSkill, Actor selectedTarget)
        {
            // 直前ステートが「スキル対象選択ステート」だった場合
            if (StateMachine.PreviousState is BattleTargetSelectionState)
            {
                // スキル実行ステートに遷移
                StateMachine.TransitionToExecuteSkillState(selectedPlayer, selectedSkill, selectedTarget);
            }
            // 直前ステートが「スキル実行ステート」だった場合
            else
            {
                StateMachine.Transition<BattleMonsterTurnState>();
            }
        }

        // このステートは一瞬で通り過ぎるので何もしない
        public override void OnInspectorGUI() { }
    }

    /// <summary>
    /// 敵ターンを処理するステート
    /// </summary>
    public class BattleMonsterTurnState : BattleStateBase
    {
        protected override void OnEnter()
        {
            // 直前ステートが「プレイヤーターン処理ステート」かつ敵が残っている場合
            if (StateMachine.PreviousState is BattlePlayerTurnState && Context.Monsters.Count > 0)
            {
                // ランダムに選択
                var user = Context.Monsters[Random.Range(0, Context.Monsters.Count)];
                var usingSkill = user.Skills[Random.Range(0, user.Skills.Count)];
                var targets = usingSkill.TargetIsMonster ? Context.Monsters : Context.Players;
                var target = targets[Random.Range(0, targets.Count)];

                // スキル実行ステートに遷移
                StateMachine.TransitionToExecuteSkillState(user, usingSkill, target);
            }
            // 直前ステートが「スキル実行ステート」だった場合
            else
            {
                StateMachine.Transition<BattleTurnEndState>();
            }
        }

        // このステートは一瞬で通り過ぎるので何もしない
        public override void OnInspectorGUI() { }
    }

    /// <summary>
    /// スキル実行ステート
    /// </summary>
    public class BattleExecuteSkillState : BattleStateBase
    {
        private Actor _user;
        private Skill _usingSkill;
        private Actor _target;

        [MarkAsOnEnter]
        public void OnEnter(Actor user, Skill usingSkill, Actor target)
        {
            _user = user;
            _usingSkill = usingSkill;
            _target = target;

            // ダメージ計算
            _target.Hp -= _usingSkill.Power;
        }

        protected override void OnExit()
        {
            // HPが0以下になっている者を退場させる
            foreach (var player in Context.Players.ToArray())
            {
                if (player.Hp <= 0) Context.Players.Remove(player);
            }
            foreach (var monster in Context.Monsters.ToArray())
            {
                if (monster.Hp <= 0) Context.Monsters.Remove(monster);
            }
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();

            // コマンド実行結果を表示
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField($"{_user.Name}の{_usingSkill.Name}");
            if (_usingSkill.Power > 0) EditorGUILayout.LabelField($"{_target.Name}に{_usingSkill.Power}ダメージ");
            else EditorGUILayout.LabelField($"{_target.Name}は{-_usingSkill.Power}回復した");
            if (_target.Hp <= 0) EditorGUILayout.LabelField($"{_target.Name}は倒れた");
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("次へ"))
            {
                StateMachine.Undo();
            }
        }
    }

    /// <summary>
    /// ターン終了ステート
    /// </summary>
    public class BattleTurnEndState : BattleStateBase
    {
        protected override void OnEnter()
        {
            // 敵が誰も生存していない場合
            if (Context.Monsters.Count == 0)
            {
                StateMachine.TransitionToClosingState(true);
            }
            // プレイヤーが誰も生存していない場合
            else if (Context.Players.Count == 0)
            {
                StateMachine.TransitionToClosingState(false);
            }
            // 両者に生存者がいる場合
            else
            {
                // 最初に戻る
                StateMachine.Transition<BattleCharacterSelectionState>();
            }
        }

        // このステートは一瞬で通り過ぎるので何もしない
        public override void OnInspectorGUI() { }
    }

    /// <summary>
    /// バトル終了ステート
    /// </summary>
    public class BattleClosingState : BattleStateBase
    {
        private bool _isWin;

        [MarkAsOnEnter]
        public void OnEnter(bool isWin)
        {
            _isWin = isWin;
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.LabelField(_isWin ? "プレイヤーは勝利した！" : "プレイヤーは敗北した...");
            EditorGUILayout.EndVertical();

            if (GUILayout.Button("終了"))
            {
                throw new BattleCloseException();
            }
        }
    }

    // バトル終了時に投げる例外
    public class BattleCloseException : System.Exception { }
}
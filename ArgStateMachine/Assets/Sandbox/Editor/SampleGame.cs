using UnityEngine;
using UnityEditor;

namespace ArgStateMachine.Sandbox
{
    /// <summary>
    /// サンプルゲームを動かすためのScriptableObject
    /// </summary>
    public class SampleGame : ScriptableObject
    {
        // 戦闘の進行状態を制御するステートマシン
        public BattleStateMachine StateMachine;
    }

    [CustomEditor(typeof(SampleGame))]
    public class SampleCasingEditor : Editor
    {
        private SampleGame _target;

        private void OnEnable()
        {
            _target = (SampleGame)target;
        }

        public override void OnInspectorGUI()
        {
            if (_target.StateMachine == null)
            {
                Setup();
            }
            else
            {
                Run();
            }
        }

        private void Setup()
        {
            if (GUILayout.Button("ゲーム開始"))
            {
                _target.StateMachine = BattleUtility.CreateStateMachine();
                _target.StateMachine.Transition<BattleCharacterSelectionState>();
            }
        }

        private void Run()
        {
            try
            {
                _target.StateMachine.CurrentState.OnInspectorGUI();
            }
            catch (BattleCloseException)
            {
                // ゲーム終了
                _target.StateMachine = null;
            }
        }
    }
}
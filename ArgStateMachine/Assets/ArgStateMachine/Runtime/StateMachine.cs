using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;

namespace ArgStateMachine
{
    /// <summary>
    /// ステートの基底クラス
    /// </summary>
    public abstract class StateBase<TState, TContext> where TState : StateBase<TState, TContext>
    {
        /// <summary> コンテキスト </summary>
        protected TContext Context => StateMachine.Context;

        /// <summary> ステートマシン </summary>
        protected StateMachine<TState, TContext> StateMachine { get; private set; }

        /// <summary> ステート遷移時イベント </summary>
        protected internal virtual void OnEnter() { }

        /// <summary> ステート更新時イベント </summary>
        protected internal virtual void OnUpdate() { }

        /// <summary> ステートから退出時イベント </summary>
        protected internal virtual void OnExit() { }

        /// <summary> ステートマシンのDisposeが呼ばれたときのイベント </summary>
        public virtual void OnStateMachineDisposed() { }

        // ステートマシンをセットする
        internal void SetStateMachine(StateMachine<TState, TContext> stateMachine)
        {
            StateMachine = stateMachine;
        }
    }

    /// <summary>
    /// ステートマシン
    /// </summary>
    public class StateMachine<TState, TContext> : IDisposable where TState : StateBase<TState, TContext>
    {
        // コンテキスト
        internal readonly TContext Context;

        // ステートの登録情報
        private readonly IReadOnlyDictionary<Type, TState> _states;

        // 遷移キュー
        protected readonly Queue<Type> TransitionQueue = new();

        // 遷移履歴
        private readonly TransitionHistroy _transitionHistroy;

        // 遷移処理実行中の間だけ立てるフラグ
        private bool _isTransitioning;

        /// <summary> 例外ハンドラ </summary>
        public event Action<Exception> ExceptionHandler;

        /// <summary> 現在のステート </summary>
        public TState CurrentState { get; private set; }
        /// <summary> 現在のステートの型 </summary>
        public Type CurrentStateType { get; private set; }
        /// <summary> 1つ前のステート </summary>
        public TState PreviousState { get; private set; }
        /// <summary> 1つ前のステートの型 </summary>
        public Type PreviousStateType { get; private set; }

        // Undo用に型定義だけしている
        private struct UndoSymbol { }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public StateMachine(TContext context, IReadOnlyList<TState> states, uint histroySize = 10, Action<Exception> exceptionHandler = null)
        {
            Context = context;
            _states = states.ToDictionary(state => state.GetType());
            _transitionHistroy = new TransitionHistroy(histroySize);

            if (exceptionHandler != null)
            {
                ExceptionHandler = exceptionHandler;
            }

            foreach (var state in states)
            {
                state.SetStateMachine(this);
            }
        }

        /// <summary>
        /// ステートマシンを更新する
        /// </summary>
        public void Update()
        {
            if (_isTransitioning) return;
            CurrentState?.OnUpdate();
        }

        /// <summary>
        /// Disposeする
        /// </summary>
        public void Dispose()
        {
            foreach (var state in _states.Values)
            {
                state.OnStateMachineDisposed();
            }
        }

        /// <summary>
        /// ステート遷移をスケジュールする
        /// </summary>
        public void ScheduleTransition<T>() where T : TState => ScheduleTransition(typeof(T));

        /// <summary>
        /// ステート遷移をスケジュールする
        /// </summary>
        public void ScheduleTransition(Type type) => TransitionQueue.Enqueue(type);

        /// <summary>
        /// 1つ前のステートに戻る遷移をスケジュールする
        /// </summary>
        public void ScheduleUndo() => TransitionQueue.Enqueue(typeof(UndoSymbol));

        /// <summary>
        /// ステート遷移を実行する
        /// </summary>
        public void Transition<T>() where T : TState => Transition(typeof(T));

        /// <summary>
        /// ステート遷移を実行する
        /// </summary>
        public void Transition(Type type)
        {
            ScheduleTransition(type);
            ExecuteTransitionQueue();
        }

        /// <summary>
        /// 1つ前のステートに戻る遷移を実行する
        /// </summary>
        public void Undo()
        {
            ScheduleUndo();
            ExecuteTransitionQueue();
        }

        /// <summary>
        /// スケジュールされた遷移を実行する
        /// </summary>
        public void ExecuteTransitionQueue()
        {
            // 既に遷移処理中なら何もしない（キューに追加さえしてあれば、既に走ってるwhileループが処理してくれる）
            if (_isTransitioning) return;

            try
            {
                _isTransitioning = true;

                // キューに積まれた遷移を順次実行
                while (TransitionQueue.Count > 0)
                {
                    var nextStateType = TransitionQueue.Dequeue();
                    TransitionImpl(nextStateType);
                }
            }
            catch (Exception e)
            {
                HandleException(e);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private void TransitionImpl(Type nextStateType)
        {
            // Undo処理である場合、遷移履歴からステートを取り出す
            bool isUndo = nextStateType == typeof(UndoSymbol);
            if (isUndo)
            {
                if (_transitionHistroy.Count == 0) return;
                nextStateType = _transitionHistroy.Pop();
            }

            if (nextStateType == null)
            {
                throw new InvalidOperationException("遷移先ステートの指定がnullです");
            }

            if (CurrentStateType == nextStateType)
            {
                throw new ArgumentException($"現在のステートと同じステートに遷移しようとしています：{nextStateType}");
            }

            if (!_states.TryGetValue(nextStateType, out var nextState))
            {
                throw new ArgumentException($"{nextStateType} というステートは登録されていません");
            }

            // 現在のステートから退出
            if (CurrentStateType != null)
            {
                CurrentState.OnExit();
                // Undo処理でなければ、遷移履歴に追加
                if (!isUndo) _transitionHistroy.Push(CurrentStateType);
            }

            // フィールド更新
            PreviousStateType = CurrentStateType;
            PreviousState = CurrentState;
            CurrentStateType = nextStateType;
            CurrentState = nextState;

            // 引数付きOnEnterが定義されているなら、そっちを優先する
            if (!PrioritizeCustomOnEnter())
            {
                // 新しいステートに入る
                CurrentState.OnEnter();
            }
        }

        private void HandleException(Exception exception)
        {
            // 例外ハンドラを呼び出す
            if (ExceptionHandler != null)
            {
                ExceptionHandler(exception);
                return;
            }

            // 例外ハンドラが設定されていない場合、例外をキャプチャして投げる
            ExceptionDispatchInfo.Capture(exception).Throw();
        }

        protected virtual bool PrioritizeCustomOnEnter() => false;
    }

    /// <summary>
    /// 遷移履歴を管理するクラス。スタック風のAPIだが、内部的には循環バッファを使っている
    /// </summary>
    internal class TransitionHistroy
    {
        // 循環バッファ
        private readonly Type[] _history;

        /// <summary> 履歴の長さ </summary>
        public int Count { get; private set; } = 0;

        // 循環バッファの始まり
        private int _start = 0;

        // 循環バッファの終わり
        private int _end = 0;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        internal TransitionHistroy(uint size)
        {
            _history = new Type[size];
        }

        internal void Push(Type state)
        {
            _history[_end] = state;
            _end = (_end + 1) % _history.Length;

            if (Count < _history.Length)
            {
                Count++;
            }
            else
            {
                // 古いデータを上書き（循環バッファ）
                _start = (_start + 1) % _history.Length;
            }
        }

        internal Type Pop()
        {
            if (Count == 0) throw new InvalidOperationException("履歴が空です");
            _end = (_end - 1 + _history.Length) % _history.Length;
            Count--;
            return _history[_end];
        }

        internal Type Peek()
        {
            if (Count == 0) return null; // 履歴が空ならnullを返す
            return _history[(_end - 1 + _history.Length) % _history.Length];
        }

        internal void Clear()
        {
            Count = 0;
            _start = 0;
            _end = 0;
        }
    }
}
using System;

namespace ArgStateMachine
{
    /// <summary>
    /// 引数付き遷移を有効化する。StateMachineを継承させたクラスに付ける
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class EnableArgTransitionAttribute : Attribute { }

    /// <summary>
    /// ステートクラスのOnEnter化したいメソッドに付ける
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class MarkAsOnEnterAttribute : Attribute { }
}
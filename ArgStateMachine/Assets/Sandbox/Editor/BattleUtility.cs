using System.Collections.Generic;

namespace ArgStateMachine.Sandbox
{
    public static class BattleUtility
    {
        /// <summary>
        /// ステートマシンの生成
        /// </summary>
        public static BattleStateMachine CreateStateMachine()
        {
            return new BattleStateMachine
            (
                CreateContext(),
                new List<BattleStateBase>()
                {
                    new BattleCharacterSelectionState(),
                    new BattleSkillSelectionState(),
                    new BattleTargetSelectionState(),
                    new BattlePlayerTurnState(),
                    new BattleMonsterTurnState(),
                    new BattleExecuteSkillState(),
                    new BattleTurnEndState(),
                    new BattleClosingState()
                }
            );
        }

        /// <summary>
        /// コンテキストの生成
        /// </summary>
        public static BattleContext CreateContext()
        {
            return new BattleContext()
            {
                // プレイヤーキャラ生成
                Players = new List<Actor>()
                {
                    new Actor()
                    {
                        Name = "戦士", Hp = 10, Skills = new List<Skill>()
                        {
                            new Skill() { Name = "たたかう", Power = 5, TargetIsMonster = true },
                            new Skill() { Name = "滅多斬り", Power = 9, TargetIsMonster = true }
                        }
                    },
                    new Actor()
                    {
                        Name = "魔法使い", Hp = 10, Skills = new List<Skill>()
                        {
                            new Skill() { Name = "ファイア", Power = 10, TargetIsMonster = true },
                            new Skill() { Name = "ヒール", Power = -7, TargetIsMonster = false }
                        }
                    }
                },

                // 敵キャラ生成
                Monsters = new List<Actor>()
                {
                    new Actor()
                    {
                        Name = "スライム", Hp = 6, Skills = new List<Skill>()
                        {
                            new Skill() { Name = "体当たり", Power = 3, TargetIsMonster = false },
                        }
                    },
                    new Actor()
                    {
                        Name = "ゴブリン", Hp = 12, Skills = new List<Skill>()
                        {
                            new Skill() { Name = "切りつける", Power = 5, TargetIsMonster = false },
                            new Skill() { Name = "突きさす", Power = 8, TargetIsMonster = false }
                        }
                    }
                }
            };
        }
    }
}

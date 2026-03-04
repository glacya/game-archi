using System;

namespace Animation
{
    public abstract class GameAction
    {
        public List<List<ActionCommand>> Children = new();
    }

    public class AnimationExecutor
    {
        public async Task Execute(GameAction action)
        {
            Task selfHandle = RunAnimation(action);

            foreach (List<GameAction> orderItems in action.Children)
            {
                await Task.WhenAll(
                    orderItems.Select(Execute)
                );
            }

            await selfHandle;
        }

        public async Task RunCommandAnimation(GameAction action)
        {
            Task animation = action switch
            {
                BeginTurnAction _ => Task.CompletedTask,
                EndTurnAction _ => Task.CompletedTask,
                SpellCastAction _ => Task.CompletedTask,
                BeforeSpellCastAction bsca => 
                    GlobalRoot.Combat.MoveToHitPosition(bsca.Caster, bsca.TargetPos),
                MidSpellCastAction _ => Task.CompletedTask,
                AfterSpellCastAction _ => Task.Delay(400),
                AttackDamageAction ada => 
                    GlobalRoot.Combat.UpdateHealth(ada.Target, ada.BeforeHp, ada.AfterHp),
                ChangeSkillPointsAction cspa =>
                    GlobalRoot.Combat.UpdateSkillPoint(cspa.BeforeSp, cspa.AfterSp),
                MoveAction mc => Task.CompletedTask,
                _ => throw new NotImplementedException(),
            };

            await animation;
        }
    }
}
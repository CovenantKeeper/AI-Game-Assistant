using RPGCharacterAnims.Lookups;

namespace RPGCharacterAnims.Actions
{
    public class Dash : InstantActionHandler<DashType>
    {
        public override bool CanStartAction(RPGCharacterController controller)
        { return controller.canAction && !controller.IsActive("Relax"); }

        protected override void _StartAction(RPGCharacterController controller, DashType dashType)
        {
            controller.GetAngry();
            controller.Dash(dashType);
        }
    }
}
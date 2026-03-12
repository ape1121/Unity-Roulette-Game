namespace Ape.Game
{
    public readonly struct GameWheelBuildRequest
    {
        public GameWheelBuildRequest(RouletteResolvedWheel wheel, bool preserveRotation)
        {
            Wheel = wheel;
            PreserveRotation = preserveRotation;
        }

        public RouletteResolvedWheel Wheel { get; }
        public bool PreserveRotation { get; }
    }
}

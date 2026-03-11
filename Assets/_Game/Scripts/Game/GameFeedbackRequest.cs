namespace Ape.Game
{
    public readonly struct GameFeedbackRequest
    {
        public GameFeedbackRequest(GameFeedbackType type, string soundName, float pitchMultiplier)
        {
            Type = type;
            SoundName = soundName;
            PitchMultiplier = pitchMultiplier;
        }

        public GameFeedbackType Type { get; }
        public string SoundName { get; }
        public float PitchMultiplier { get; }

        public static GameFeedbackRequest CreateSound(string soundName, float pitchMultiplier = 1f)
        {
            return new GameFeedbackRequest(GameFeedbackType.PlaySound, soundName, pitchMultiplier);
        }

        public static GameFeedbackRequest CreateSpinStartShake()
        {
            return new GameFeedbackRequest(GameFeedbackType.SpinStartShake, string.Empty, 1f);
        }

        public static GameFeedbackRequest CreateBombShake()
        {
            return new GameFeedbackRequest(GameFeedbackType.BombShake, string.Empty, 1f);
        }
    }
}

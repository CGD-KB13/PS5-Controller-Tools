
namespace PS5_Controller_Tools.Triggers
{
    internal static class TriggerInputReader
    {
        public static TriggerState ReadLeft(ControllerStateSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            return new TriggerState(snapshot.LeftTriggerPressure);
        }

        public static TriggerState ReadRight(ControllerStateSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));

            return new TriggerState(snapshot.RightTriggerPressure);
        }
    }
}

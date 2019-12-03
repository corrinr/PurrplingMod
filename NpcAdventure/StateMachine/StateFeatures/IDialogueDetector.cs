using StardewValley;

namespace NpcAdventure.StateMachine.StateFeatures
{
    internal interface IDialogueDetector
    {
        void OnDialogueSpeaked(string question, string answer);
    }
}